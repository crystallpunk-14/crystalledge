using Content.Client._CE.UserInterface.Systems.HealthMana.Widgets;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.GameObjects;
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
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobThresholdChecked>(OnMobThresholdChecked);
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

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        UpdateHealth(args.Target, args.Component);
    }

    private void OnMobThresholdChecked(ref MobThresholdChecked args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        UpdateHealth(args.Target, args.MobState, args.Damageable, args.Threshold);
    }

    private void UpdateHealth(
        EntityUid uid,
        MobStateComponent? mobState = null,
        DamageableComponent? damageable = null,
        MobThresholdsComponent? thresholds = null)
    {
        if (_healthBar == null)
            return;

        if (_player.LocalEntity is not { } local || uid != local)
        {
            _healthBar.Visible = false;
            return;
        }

        if (mobState == null && !EntityManager.TryGetComponent(uid, out mobState) ||
            damageable == null && !EntityManager.TryGetComponent(uid, out damageable) ||
            thresholds == null && !EntityManager.TryGetComponent(uid, out thresholds))
        {
            _healthBar.Visible = false;
            return;
        }

        _healthBar.Visible = true;
        _healthBar.UpdateHealthDisplay(uid, mobState, damageable, thresholds);
    }
}
