using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects.IncomingHealBonus;

public sealed partial class CEIncomingHealBonusStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEIncomingHealBonusStatusEffectComponent, StatusEffectRelayedEvent<CEGetIncomingHealEvent>>(OnIncomingHeal);
    }

    private void OnIncomingHeal(Entity<CEIncomingHealBonusStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEGetIncomingHealEvent> args)
    {
        var bonus = ent.Comp.BonusPerStack;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            bonus *= stackComp.Stacks;

        args.Args.HealAmount += bonus;
    }
}
