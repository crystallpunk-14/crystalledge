using Content.Client._CE.UserInterface.Systems.HealthMana.Widgets;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;

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
        SubscribeLocalEvent<CEHealthChangedEvent>(OnHealthChanged);
        SubscribeLocalEvent<CEMobStateChangedEvent>(OnMobStateChanged);
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

    private void OnHealthChanged(CEHealthChangedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        UpdateHealth(args.Target);
    }

    private void OnMobStateChanged(CEMobStateChangedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        UpdateHealth(args.Target);
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

        if (!EntityManager.TryGetComponent<CEHealthComponent>(uid, out var health))
        {
            _healthBar.Visible = false;
            return;
        }

        _healthBar.Visible = true;
        _healthBar.UpdateHealthDisplay(health);
    }
}
