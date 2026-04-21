using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.Skill.Skills.BonusIncomingMana;

public sealed partial class CEBonusIncomingManaStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBonusIncomingManaStatusEffectComponent, StatusEffectRelayedEvent<CEGetManaRestoringAmountEvent>>(OnRestore);
    }

    private void OnRestore(Entity<CEBonusIncomingManaStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEGetManaRestoringAmountEvent> args)
    {
        var amount = args.Args.RestoreAmount;

        var bonus = ent.Comp.Amount;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            bonus *= stackComp.Stacks;

        amount = amount + bonus;

        args.Args.RestoreAmount = amount;
    }
}
