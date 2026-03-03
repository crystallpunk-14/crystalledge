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

public abstract partial class CESharedMobStateSystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    /// <summary>
    /// Movement speed multiplier when in Critical state (crawling).
    /// </summary>
    private const float CriticalSpeedModifier = 0.15f;

    private void SubscribeActionBlockerEvents()
    {
        // Critical blocks most actions except movement and speech
        SubscribeLocalEvent<CEMobStateComponent, ChangeDirectionAttemptEvent>(OnChangeDirectionAttempt);
        SubscribeLocalEvent<CEMobStateComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<CEMobStateComponent, UseAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, AttackAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, ThrowAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, DropAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, PickupAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, StartPullAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, StandAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, PointAttemptEvent>(OnBlockIfIncapacitated);
        SubscribeLocalEvent<CEMobStateComponent, SpeakAttemptEvent>(OnSpeakAttempt);
        SubscribeLocalEvent<CEMobStateComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<CEMobStateComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);

        // Movement speed reduction in Critical
        SubscribeLocalEvent<CEMobStateComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);

        // Refresh movement speed on state change
        SubscribeLocalEvent<CEMobStateComponent, CEMobStateChangedEvent>(OnMobStateChangedSpeed);
    }

    /// <summary>
    /// Direction changes are allowed in Critical (for crawling) but blocked in Dead.
    /// </summary>
    private void OnChangeDirectionAttempt(EntityUid uid, CEMobStateComponent comp, ChangeDirectionAttemptEvent args)
    {
        if (comp.CurrentState == CEMobState.Dead)
            args.Cancel();
    }

    /// <summary>
    /// Movement is allowed in Critical (slow crawl) but blocked in Dead.
    /// </summary>
    private void OnUpdateCanMove(EntityUid uid, CEMobStateComponent comp, UpdateCanMoveEvent args)
    {
        if (comp.CurrentState == CEMobState.Dead)
            args.Cancel();
    }

    /// <summary>
    /// Speech is allowed in Critical (whisper only) but blocked in Dead.
    /// TODO: Force whisper mode when in Critical state via the chat/speech pipeline.
    /// </summary>
    private void OnSpeakAttempt(EntityUid uid, CEMobStateComponent comp, SpeakAttemptEvent args)
    {
        if (comp.CurrentState == CEMobState.Dead)
            args.Cancel();
    }

    /// <summary>
    /// Blocks the action if the entity is in Critical or Dead state.
    /// </summary>
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

    /// <summary>
    /// Applies heavy movement speed penalty when in Critical state.
    /// </summary>
    private void OnRefreshMoveSpeed(EntityUid uid, CEMobStateComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.CurrentState == CEMobState.Critical)
            args.ModifySpeed(CriticalSpeedModifier, CriticalSpeedModifier);
    }

    /// <summary>
    /// Refresh movement speed modifiers when state changes.
    /// </summary>
    private void OnMobStateChangedSpeed(EntityUid uid, CEMobStateComponent comp, CEMobStateChangedEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }
}
