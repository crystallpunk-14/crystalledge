using Content.Shared.ActionBlocker;
using Content.Shared.Actions.Events;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Events;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.StatusEffects.ActionBlocker;

public sealed class CEActionBlockerStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<ActionAttemptEvent>>(OnActionUse);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<UseAttemptEvent>>(OnUseAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<AttackAttemptEvent>>(OnAttackAttempt);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRelayedEvent<UpdateCanMoveEvent>>(OnBlockMove);

        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<CEActionBlockerStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        if (ent.Comp.BlockMove)
            _actionBlocker.UpdateCanMove(status.AppliedTo.Value);
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

    private void OnActionUse(Entity<CEActionBlockerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<ActionAttemptEvent> args)
    {
        if (!ent.Comp.BlockActions)
            return;

        args.Args = args.Args with { Cancelled = true };
    }
}
