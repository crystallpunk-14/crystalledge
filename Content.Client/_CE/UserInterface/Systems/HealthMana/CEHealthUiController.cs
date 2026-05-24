using Content.Client._CE.UserInterface.Systems.HealthMana.Widgets;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CE.UserInterface.Systems.HealthMana;

[UsedImplicitly]
public sealed class CEHealthUiController : UIController
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private CEHealthUI? _healthBar;

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
            UpdateHealth(player);
    }

    private void OnScreenLoad()
    {
        _healthBar = GetHealthBar();

        if (_healthBar == null)
            return;

        if (_player.LocalEntity is { } player)
            UpdateHealth(player);
        else
            _healthBar.Visible = false;
    }

    private void OnScreenUnload()
    {
        if (_healthBar != null)
            _healthBar.Visible = false;

        _healthBar = null;
    }

    private CEHealthUI? GetHealthBar()
    {
        if (UIManager.ActiveScreen is DefaultGameScreen game)
            return game.HealthBar;

        if (UIManager.ActiveScreen is SeparatedChatGameScreen separated)
            return separated.HealthBar;

        return null;
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        _healthBar ??= GetHealthBar();
        UpdateHealth(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (_healthBar != null)
            _healthBar.Visible = false;
    }

    private void UpdateHealth(EntityUid uid)
    {
        if (_healthBar == null)
            return;

        if (_player.LocalEntity is not { } local || uid != local)
        {
            _healthBar.Visible = false;
            return;
        }

        if (!EntityManager.TryGetComponent<CEDamageableComponent>(uid, out _))
        {
            _healthBar.Visible = false;
            return;
        }

        var damageable = EntityManager.System<CESharedDamageableSystem>();
        var info = damageable.GetHealthInfo(uid);

        if (info.MaxHp <= 0)
        {
            _healthBar.Visible = false;
            return;
        }

        _healthBar.Visible = true;
        _healthBar.UpdateHealthDisplay(info);
    }
}
