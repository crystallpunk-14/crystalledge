using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.Skill.Skills.EffectiveHeal;

public sealed partial class CEEffectiveHealingStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEffectiveHealingStatusEffectComponent, StatusEffectRelayedEvent<CEGetHealAmountEvent>>(OnGetHealAmount);
    }

    private void OnGetHealAmount(Entity<CEEffectiveHealingStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEGetHealAmountEvent> args)
    {
        var count = ent.Comp.AdditionalHeal;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            count *= stackComp.Stacks;

        args.Args.HealAmount += count;
    }
}
