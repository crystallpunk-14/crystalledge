using Content.Client._CE.UserInterface.Systems.HealthMana.Widgets;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._CE.Stamina;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CE.UserInterface.Systems.HealthMana;

[UsedImplicitly]
public sealed class CEStaminaUiController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private CEStaminaSystem? _staminaSystem;
    private CEStaminaUI? _staminaBar;

    public override void Initialize()
    {
        base.Initialize();
        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public void OnStateEntered(GameplayState state)
    {
        _staminaSystem = EntityManager.System<CEStaminaSystem>();
    }

    public void OnStateExited(GameplayState state)
    {
        _staminaSystem = null;
    }

    private void OnScreenLoad()
    {
        _staminaBar = GetStaminaBar();

        if (_staminaBar == null)
            return;

        if (_player.LocalEntity is { } player)
            UpdateStamina(player);
        else
            _staminaBar.Visible = false;
    }

    private void OnScreenUnload()
    {
        if (_staminaBar != null)
            _staminaBar.Visible = false;

        _staminaBar = null;
    }

    private CEStaminaUI? GetStaminaBar()
    {
        if (UIManager.ActiveScreen is DefaultGameScreen game)
            return game.StaminaBar;

        if (UIManager.ActiveScreen is SeparatedChatGameScreen separated)
            return separated.StaminaBar;

        return null;
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        _staminaBar ??= GetStaminaBar();
        UpdateStamina(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (_staminaBar != null)
            _staminaBar.Visible = false;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_player.LocalEntity is { } player)
            UpdateStamina(player);
    }

    private void UpdateStamina(EntityUid uid)
    {
        if (_staminaBar == null)
            return;

        if (_player.LocalEntity is not { } local || uid != local)
        {
            _staminaBar.Visible = false;
            return;
        }

        if (!EntityManager.TryGetComponent<CEStaminaComponent>(uid, out var stamina))
        {
            _staminaBar.Visible = false;
            return;
        }

        _staminaBar.Visible = true;

        var current = _staminaSystem?.GetStamina((uid, stamina)) ?? stamina.Stamina;
        var max = stamina.MaxStamina;
        var ratio = max > 0 ? Math.Clamp(current / max, 0f, 1f) : 0f;

        _staminaBar.SetStamina(ratio, (int) MathF.Round(current), (int) MathF.Round(max), stamina.Exhausted);
    }
}
