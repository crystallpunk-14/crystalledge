using Content.Shared._CE.ScreenPopup;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Localization;

namespace Content.Client._CE.ScreenPopup;

/// <summary>
/// Listens for <see cref="CEScreenPopupShowEvent"/> network events and drives the
/// <see cref="CEScreenPopupControl"/> queue, playing popups one at a time.
/// </summary>
public sealed class CEClientScreenPopupSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IUserInterfaceManager _userInterface = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    private CEScreenPopupControl _ui = default!;
    private bool _remove;

    private readonly Queue<CEScreenPopupShowEvent> _queue = new();
    private bool _isPlaying;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CEScreenPopupShowEvent>(OnScreenPopup);

        _ui = new CEScreenPopupControl();
        _ui.OnAnimationEnd += OnAnimationEnd;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_remove)
            return;

        _userInterface.RootControl.RemoveChild(_ui);
        _remove = false;
    }

    private void OnScreenPopup(CEScreenPopupShowEvent ev)
    {
        if (_player.LocalEntity is null)
            return;

        _queue.Enqueue(ev);

        if (!_isPlaying)
            PlayNext();
    }

    private void OnAnimationEnd()
    {
        PlayNext();
    }

    private void PlayNext()
    {
        if (_queue.Count == 0)
        {
            _isPlaying = false;
            _remove = true;
            return;
        }

        var ev = _queue.Dequeue();

        if (ev.Sound is not null && _player.LocalEntity is not null)
            _audio.PlayGlobal(ev.Sound, _player.LocalEntity.Value);

        if (_ui.Parent is null)
            _userInterface.RootControl.AddChild(_ui);

        _remove = false;
        _isPlaying = true;

        // Resolve localized strings per the client's locale.
        var title = ev.Title.HasValue ? _loc.GetString(ev.Title.Value) : string.Empty;
        var desc = ev.Desc.HasValue ? _loc.GetString(ev.Desc.Value) : string.Empty;

        _ui.AnimationStart(title, desc);
    }
}
