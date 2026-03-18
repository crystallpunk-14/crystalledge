using Content.Shared._CE.Health.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Pointing;
using Content.Shared.Pulling.Events;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Throwing;

namespace Content.Shared._CE.Health;

public abstract partial class CESharedHealthSystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    /// <summary>
    /// Movement speed multiplier when in Critical state (crawling).
    /// </summary>
    private const float CriticalSpeedModifier = 0.2f;

    private void InitBlocker()
    {
        // Critical blocks most actions except movement and speech
        SubscribeLocalEvent<CEHealthComponent, ChangeDirectionAttemptEvent>(OnBlockIfDead);
        SubscribeLocalEvent<CEHealthComponent, UpdateCanMoveEvent>(OnBlockIfDead);
        SubscribeLocalEvent<CEHealthComponent, UseAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEHealthComponent, AttackAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEHealthComponent, ThrowAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEHealthComponent, DropAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEHealthComponent, PickupAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEHealthComponent, StartPullAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEHealthComponent, StandAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEHealthComponent, PointAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEHealthComponent, SpeakAttemptEvent>(OnBlockIfDead);
        SubscribeLocalEvent<CEHealthComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<CEHealthComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);

        // Movement speed reduction in Critical
        SubscribeLocalEvent<CEHealthComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);

        // Refresh movement speed on state change
        SubscribeLocalEvent<CEHealthComponent, CEMobStateChangedEvent>(OnMobStateChangedSpeed);
    }


    /// <summary>
    /// Blocks the action if the entity is in Critical or Dead state.
    /// </summary>
    private void OnBlockIfIncapacitated(EntityUid uid, CEHealthComponent comp, CancellableEntityEventArgs args)
    {
        if (comp.CurrentState is CEMobState.Critical or CEMobState.Dead)
            args.Cancel();
    }

    /// <summary>
    /// Blocks the action if the entity is in the Dead state.
    /// </summary>
    private void OnBlockIfDead(EntityUid uid, CEHealthComponent comp, CancellableEntityEventArgs args)
    {
        if (comp.CurrentState is CEMobState.Dead)
            args.Cancel();
    }

    private void OnEquipAttempt(EntityUid uid, CEHealthComponent comp, IsEquippingAttemptEvent args)
    {
        if (args.Equipee == uid)
            OnBlockIfIncapacitated(uid, comp, args);
    }

    private void OnUnequipAttempt(EntityUid uid, CEHealthComponent comp, IsUnequippingAttemptEvent args)
    {
        if (args.Unequipee == uid)
            OnBlockIfIncapacitated(uid, comp, args);
    }

    /// <summary>
    /// Applies heavy movement speed penalty when in Critical state.
    /// </summary>
    private void OnRefreshMoveSpeed(EntityUid uid, CEHealthComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.CurrentState == CEMobState.Critical)
            args.ModifySpeed(CriticalSpeedModifier, CriticalSpeedModifier);
    }

    /// <summary>
    /// Refresh movement speed modifiers when state changes.
    /// </summary>
    private void OnMobStateChangedSpeed(EntityUid uid, CEHealthComponent comp, CEMobStateChangedEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }
}
