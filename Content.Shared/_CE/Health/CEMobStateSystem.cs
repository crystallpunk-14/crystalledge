using Content.Shared._CE.Damage;
using Content.Shared._CE.Health.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Pointing;
using Content.Shared.Pulling.Events;
using Content.Shared.Rejuvenate;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Health;

/// <summary>
/// Manages CE mob states (Alive, Critical) based on <see cref="CEDamageableComponent"/> damage
/// and thresholds in <see cref="CEMobStateComponent"/>.
/// </summary>
public sealed partial class CEMobStateSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    private const float CriticalSpeedModifier = 0.2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMobStateComponent, CEDamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CEMobStateComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEMobStateComponent, RejuvenateEvent>(OnRejuvenate);

        // Action blocking
        SubscribeLocalEvent<CEMobStateComponent, ChangeDirectionAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, UpdateCanMoveEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, UseAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, AttackAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, ThrowAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, DropAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, PickupAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, StartPullAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, StandAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, PointAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, SpeakAttemptEvent>(OnBlockIfCritical);
        SubscribeLocalEvent<CEMobStateComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<CEMobStateComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<CEMobStateComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<CEMobStateComponent, CEMobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnRejuvenate(Entity<CEMobStateComponent> ent, ref RejuvenateEvent args)
    {
        // Damage is reset by CESharedDamageableSystem; we just need to force re-evaluate state.
        if (!TryComp<CEDamageableComponent>(ent, out var dmg))
            return;

        UpdateState(ent, ent.Comp, dmg.TotalDamage);
    }

    private void OnStartup(Entity<CEMobStateComponent> ent, ref ComponentStartup args)
    {
        var damage = 0;
        if (TryComp<CEDamageableComponent>(ent, out var dmg))
            damage = dmg.TotalDamage;

        UpdateState(ent, ent.Comp, damage);
        SetDamageFraction(ent, ent.Comp, damage);
    }

    private void OnDamageChanged(Entity<CEMobStateComponent> ent, ref CEDamageChangedEvent args)
    {
        UpdateState(ent, ent.Comp, args.NewDamage);
        SetDamageFraction(ent, ent.Comp, args.NewDamage);
    }

    private void UpdateState(Entity<CEMobStateComponent> ent, CEMobStateComponent mobState, int totalDamage)
    {
        var newState = CalculateState(mobState, totalDamage);

        if (newState == mobState.CurrentState)
            return;

        var oldState = mobState.CurrentState;
        mobState.CurrentState = newState;
        Dirty(ent);

        _appearance.SetData(ent, CEMobStateVisuals.State, newState);

        if (!_timing.ApplyingState)
        {
            var ev = new CEMobStateChangedEvent(ent, oldState, newState);
            RaiseLocalEvent(ent, ev, true);
        }
    }

    private CEMobState CalculateState(CEMobStateComponent mobState, int totalDamage)
    {
        if (totalDamage >= mobState.CriticalThreshold)
            return CEMobState.Critical;

        return CEMobState.Alive;
    }

    private void SetDamageFraction(EntityUid ent, CEMobStateComponent mobState, int totalDamage)
    {
        var fraction = mobState.CriticalThreshold > 0
            ? Math.Clamp((float) totalDamage / mobState.CriticalThreshold, 0f, 1f)
            : 0f;
        _appearance.SetData(ent, CEDamageVisuals.DamageFraction, fraction);
    }

    private void OnStateEntered(EntityUid target, CEMobState state)
    {
        _blocker.UpdateCanMove(target);

        switch (state)
        {
            case CEMobState.Alive:
                _standing.Stand(target);
                break;
            case CEMobState.Critical:
                _standing.Down(target);
                var dropEv = new DropHandItemsEvent();
                RaiseLocalEvent(target, ref dropEv);
                break;
        }
    }

    private void OnStateExited(EntityUid target, CEMobState state)
    {
        switch (state)
        {
            case CEMobState.Critical:
                _standing.Stand(target);
                break;
        }
    }

    public void SetThresholds(Entity<CEMobStateComponent?> ent, int criticalThreshold)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.CriticalThreshold = criticalThreshold;
        Dirty(ent);

        var damage = 0;
        if (TryComp<CEDamageableComponent>(ent, out var dmg))
            damage = dmg.TotalDamage;

        UpdateState((ent, ent.Comp), ent.Comp, damage);
    }

    #region State Queries

    public bool IsAlive(EntityUid uid, CEMobStateComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.CurrentState == CEMobState.Alive;
    }

    public bool IsCritical(EntityUid uid, CEMobStateComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.CurrentState == CEMobState.Critical;
    }

    public bool IsIncapacitated(EntityUid uid, CEMobStateComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.CurrentState == CEMobState.Critical;
    }

    /// <summary>
    /// Returns the health fraction (0..1) of the entity, where 1 is full health and 0 is at critical threshold.
    /// Returns 1 if no damageable/mob state components are found.
    /// </summary>
    public float GetHealthFraction(EntityUid uid, CEDamageableComponent? damage = null, CEMobStateComponent? mobState = null)
    {
        if (!Resolve(uid, ref damage, false) || !Resolve(uid, ref mobState, false))
            return 1f;

        return mobState.CriticalThreshold > 0
            ? 1f - (float) damage.TotalDamage / mobState.CriticalThreshold
            : 1f;
    }

    #endregion

    #region Action Blocking

    private void OnBlockIfCritical(EntityUid uid, CEMobStateComponent comp, CancellableEntityEventArgs args)
    {
        if (comp.CurrentState == CEMobState.Critical)
            args.Cancel();
    }

    private void OnEquipAttempt(EntityUid uid, CEMobStateComponent comp, IsEquippingAttemptEvent args)
    {
        if (args.Equipee == uid)
            OnBlockIfCritical(uid, comp, args);
    }

    private void OnUnequipAttempt(EntityUid uid, CEMobStateComponent comp, IsUnequippingAttemptEvent args)
    {
        if (args.Unequipee == uid)
            OnBlockIfCritical(uid, comp, args);
    }

    private void OnRefreshMoveSpeed(EntityUid uid, CEMobStateComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.CurrentState == CEMobState.Critical)
            args.ModifySpeed(CriticalSpeedModifier, CriticalSpeedModifier);
    }

    private void OnMobStateChanged(EntityUid uid, CEMobStateComponent comp, CEMobStateChangedEvent args)
    {
        OnStateExited(uid, args.OldState);
        OnStateEntered(uid, args.NewState);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    #endregion
}

/// <summary>
/// Raised when a CE mob state changes.
/// </summary>
public sealed class CEMobStateChangedEvent(EntityUid target, CEMobState oldState, CEMobState newState)
    : EntityEventArgs
{
    public readonly EntityUid Target = target;
    public readonly CEMobState OldState = oldState;
    public readonly CEMobState NewState = newState;
}
