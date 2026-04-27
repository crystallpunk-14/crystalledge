using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects.Immunity;

/// <summary>
/// Handles <see cref="CEStatusEffectImmunityComponent"/>: cancels application of
/// blocked status effects when relayed via the StatusEffectNew relay system.
/// </summary>
public sealed class CEStatusEffectImmunitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStatusEffectImmunityComponent, StatusEffectRelayedEvent<CEAttemptApplyStatusEffectEvent>>(OnAttemptApply);
        SubscribeLocalEvent<CEStatusEffectImmunityComponent, StatusEffectRelayedEvent<CEAttemptApplyStatusEffectStackEvent>>(OnAttemptApplyStack);
    }

    private void OnAttemptApply(Entity<CEStatusEffectImmunityComponent> ent, ref StatusEffectRelayedEvent<CEAttemptApplyStatusEffectEvent> args)
    {
        if (!ent.Comp.BlockedEffects.Contains(args.Args.StatusEffect))
            return;

        args.Args.Cancelled = true;
    }

    private void OnAttemptApplyStack(Entity<CEStatusEffectImmunityComponent> ent, ref StatusEffectRelayedEvent<CEAttemptApplyStatusEffectStackEvent> args)
    {
        if (!ent.Comp.BlockedEffects.Contains(args.Args.StatusEffect))
            return;

        args.Args.Cancelled = true;
    }
}
