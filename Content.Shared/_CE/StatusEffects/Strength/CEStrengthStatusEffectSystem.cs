using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects.Strength;

public sealed class CEStrengthStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStrengthStatusEffectComponent, StatusEffectRelayedEvent<CEOutgoingDamageCalculateEvent>>(OnOutgoingDamage);
    }

    private void OnOutgoingDamage(
        Entity<CEStrengthStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CEOutgoingDamageCalculateEvent> args)
    {
        if (args.Args.Cancelled || args.Args.AttackType != CEAttackType.Melee)
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
