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
/// Manages CE mob states (Alive, Critical, Dead) based on <see cref="CEDamageableComponent"/> damage
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
        SubscribeLocalEvent<CEMobStateComponent, ChangeDirectionAttemptEvent>(OnBlockIfDead);
        SubscribeLocalEvent<CEMobStateComponent, UpdateCanMoveEvent>(OnBlockIfDead);
        SubscribeLocalEvent<CEMobStateComponent, UseAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, AttackAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, ThrowAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, DropAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, PickupAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, StartPullAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, StandAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, PointAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, SpeakAttemptEvent>(OnBlockIfDead);
        SubscribeLocalEvent<CEMobStateComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<CEMobStateComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<CEMobStateComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<CEMobStateComponent, CEMobStateChangedEvent>(OnMobStateChangedSpeed);
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
    }

    private void OnDamageChanged(Entity<CEMobStateComponent> ent, ref CEDamageChangedEvent args)
    {
        UpdateState(ent, ent.Comp, args.NewDamage);
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
            OnStateExited(ent, oldState);
            OnStateEntered(ent, newState);

            var ev = new CEMobStateChangedEvent(ent, oldState, newState);
            RaiseLocalEvent(ent, ev, true);
        }
    }

    private CEMobState CalculateState(CEMobStateComponent mobState, int totalDamage)
    {
        if (totalDamage >= mobState.DeadThreshold)
            return CEMobState.Dead;

        if (totalDamage >= mobState.CriticalThreshold)
            return CEMobState.Critical;

        return CEMobState.Alive;
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
            case CEMobState.Dead:
                _standing.Down(target);
                var dropDeadEv = new DropHandItemsEvent();
                RaiseLocalEvent(target, ref dropDeadEv);
                break;
        }
    }

    private void OnStateExited(EntityUid target, CEMobState state)
    {
        switch (state)
        {
            case CEMobState.Critical:
            case CEMobState.Dead:
                _standing.Stand(target);
                break;
        }
    }

    public void SetThresholds(Entity<CEMobStateComponent?> ent, int criticalThreshold, int deadThreshold)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.CriticalThreshold = criticalThreshold;
        ent.Comp.DeadThreshold = deadThreshold;
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

    public bool IsDead(EntityUid uid, CEMobStateComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.CurrentState == CEMobState.Dead;
    }

    public bool IsIncapacitated(EntityUid uid, CEMobStateComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.CurrentState is CEMobState.Critical or CEMobState.Dead;
    }

    #endregion

    #region Action Blocking

    private void OnBlockIfDead(EntityUid uid, CEMobStateComponent comp, CancellableEntityEventArgs args)
    {
        if (comp.CurrentState is CEMobState.Dead)
            args.Cancel();
    }

    private void OnBlockIfIncapacitated(EntityUid uid, CEMobStateComponent comp, CancellableEntityEventArgs args)
    {
        if (comp.CurrentState is CEMobState.Critical or CEMobState.Dead)
            args.Cancel();
    }

    private void OnEquipAttempt(EntityUid uid, CEMobStateComponent comp, IsEquippingAttemptEvent args)
    {
        if (args.Equipee == uid)
            OnBlockIfIncapacitated(uid, comp, args);
    }

    private void OnUnequipAttempt(EntityUid uid, CEMobStateComponent comp, IsUnequippingAttemptEvent args)
    {
        if (args.Unequipee == uid)
            OnBlockIfIncapacitated(uid, comp, args);
    }

    private void OnRefreshMoveSpeed(EntityUid uid, CEMobStateComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.CurrentState == CEMobState.Critical)
            args.ModifySpeed(CriticalSpeedModifier, CriticalSpeedModifier);
    }

    private void OnMobStateChangedSpeed(EntityUid uid, CEMobStateComponent comp, CEMobStateChangedEvent args)
    {
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
