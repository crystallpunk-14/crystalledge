using Content.Client._CE.UserInterface.Systems.HealthMana.Widgets;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._CE.Soul;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CE.UserInterface.Systems.HealthMana;

[UsedImplicitly]
public sealed partial class CESoulUiController : UIController
{
    [Dependency] private IPlayerManager _player = default!;

    private CESoulUI? _soulBar;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_player.LocalEntity is { } player)
            UpdateSouls(player);
    }

    private void OnScreenLoad()
    {
        _soulBar = GetSoulBar();

        if (_soulBar == null)
            return;

        if (_player.LocalEntity is { } player)
            UpdateSouls(player);
        else
            _soulBar.Visible = false;
    }

    private void OnScreenUnload()
    {
        if (_soulBar != null)
            _soulBar.Visible = false;

        _soulBar = null;
    }

    private CESoulUI? GetSoulBar()
    {
        if (UIManager.ActiveScreen is DefaultGameScreen game)
            return game.SoulBar;

        if (UIManager.ActiveScreen is SeparatedChatGameScreen separated)
            return separated.SoulBar;

        return null;
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        _soulBar ??= GetSoulBar();
        UpdateSouls(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (_soulBar != null)
            _soulBar.Visible = false;
    }

    private void UpdateSouls(EntityUid uid, Shared._CE.Soul.Components.CESoulContainerComponent? container = null)
    {
        if (_soulBar == null)
            return;

        if (_player.LocalEntity is not { } local || uid != local)
        {
            _soulBar.Visible = false;
            return;
        }

        if (container == null && !EntityManager.TryGetComponent(uid, out container))
        {
            _soulBar.Visible = false;
            return;
        }

        _soulBar.Visible = true;
        _soulBar.SetSouls(container.Souls);
    }
}
