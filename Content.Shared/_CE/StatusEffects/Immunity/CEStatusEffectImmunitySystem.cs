using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects.Immunity;

/// <summary>
/// Handles <see cref="CEStatusEffectImmunityComponent"/>: cancels incoming status effects
/// by subscribing to target-side attempt events relayed via the StatusEffectNew relay system.
///
/// Target-side events (<see cref="CEAttemptReceiveStatusEffectEvent"/>, <see cref="CEAttemptReceiveStatusEffectStackEvent"/>)
/// are raised on the entity receiving the effect — this is the correct place for target immunity.
/// </summary>
public sealed class CEStatusEffectImmunitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStatusEffectImmunityComponent, StatusEffectRelayedEvent<CEAttemptReceiveStatusEffectEvent>>(OnReceive);
        SubscribeLocalEvent<CEStatusEffectImmunityComponent, StatusEffectRelayedEvent<CEAttemptReceiveStatusEffectStackEvent>>(OnReceiveStack);
    }

    private void OnReceive(Entity<CEStatusEffectImmunityComponent> ent, ref StatusEffectRelayedEvent<CEAttemptReceiveStatusEffectEvent> args)
    {
        if (!ent.Comp.BlockedEffects.Contains(args.Args.StatusEffect))
            return;

        args.Args.Cancelled = true;
    }

    private void OnReceiveStack(Entity<CEStatusEffectImmunityComponent> ent, ref StatusEffectRelayedEvent<CEAttemptReceiveStatusEffectStackEvent> args)
    {
        if (!ent.Comp.BlockedEffects.Contains(args.Args.StatusEffect))
            return;

        args.Args.Cancelled = true;
    }
}
