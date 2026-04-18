using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects.BonusDamage;

public sealed class CEBonusDamageStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBonusDamageStatusEffectComponent, StatusEffectRelayedEvent<CEOutgoingDamageCalculateEvent>>(OnOutgoingDamage);
    }

    private void OnOutgoingDamage(
        Entity<CEBonusDamageStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CEOutgoingDamageCalculateEvent> args)
    {
        if (args.Args.Cancelled || !ent.Comp.AttackTypes.Contains(args.Args.AttackType))
            return;

        if (!TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            return;

        foreach (var (type, bonus) in ent.Comp.BonusDamagePerStack.Types)
        {
            if (bonus <= 0)
                continue;

            if (!args.Args.Damage.Types.TryGetValue(type, out var existing) || existing <= 0)
                continue;

            args.Args.Damage.Types[type] = existing + bonus * stackComp.Stacks;
        }
    }
}
