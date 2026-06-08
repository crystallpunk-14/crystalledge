using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Pulling.Events;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Throwing;

namespace Content.Shared._CE.StatusEffects.ActionBlocker;

public sealed partial class CEActionBlockerStatusEffectSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<UseAttemptEvent>>(OnUseAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<AttackAttemptEvent>>(OnAttackAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<UpdateCanMoveEvent>>(OnBlockMove);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<ThrowAttemptEvent>>(OnThrowAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<DropAttemptEvent>>(OnDropAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<PickupAttemptEvent>>(OnPickupAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<StartPullAttemptEvent>>(OnPullAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<StandAttemptEvent>>(OnStandAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<IsEquippingAttemptEvent>>(OnEquipAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<IsUnequippingAttemptEvent>>(OnUnequipAttempt);

        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        if (ent.Comp.BlockMove)
            _actionBlocker.UpdateCanMove(status.AppliedTo.Value);

        if (ent.Comp.BlockAttack && TryComp<CombatModeComponent>(status.AppliedTo.Value, out var combatComp))
            _combat.SetInCombatMode(status.AppliedTo.Value, false, combatComp);
    }

    private void OnRemoved(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        _actionBlocker.UpdateCanMove(status.AppliedTo.Value);
    }

    private void OnBlockMove(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<UpdateCanMoveEvent> args)
    {
        if (!ent.Comp.BlockMove)
            return;

        args.Args.Cancel();
    }

    private void OnAttackAttempt(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<AttackAttemptEvent> args)
    {
        if (!ent.Comp.BlockAttack)
            return;

        args.Args.Cancel();
    }

    private void OnUseAttempt(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<UseAttemptEvent> args)
    {
        if (!ent.Comp.BlockUse)
            return;

        args.Args.Cancel();
    }

    private void OnThrowAttempt(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<ThrowAttemptEvent> args)
    {
        if (ent.Comp.BlockThrow)
            args.Args.Cancel();
    }

    private void OnDropAttempt(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<DropAttemptEvent> args)
    {
        if (ent.Comp.BlockDrop)
            args.Args.Cancel();
    }

    private void OnPickupAttempt(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<PickupAttemptEvent> args)
    {
        if (ent.Comp.BlockPickup)
            args.Args.Cancel();
    }

    private void OnPullAttempt(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<StartPullAttemptEvent> args)
    {
        if (ent.Comp.BlockPull)
            args.Args.Cancel();
    }

    private void OnStandAttempt(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<StandAttemptEvent> args)
    {
        if (ent.Comp.BlockStand)
            args.Args.Cancel();
    }

    private void OnEquipAttempt(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<IsEquippingAttemptEvent> args)
    {
        if (ent.Comp.BlockEquip)
            args.Args.Cancel();
    }

    private void OnUnequipAttempt(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<IsUnequippingAttemptEvent> args)
    {
        if (ent.Comp.BlockUnequip)
            args.Args.Cancel();
    }
}
