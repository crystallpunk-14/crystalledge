using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.Skill.Skills.HealingWaters;

public sealed partial class CEHealingWatersStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEHealingWatersStatusEffectComponent, StatusEffectRelayedEvent<CEGetHealAmountEvent>>(OnGetHealAmount);
    }

    private void OnGetHealAmount(Entity<CEHealingWatersStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEGetHealAmountEvent> args)
    {
        var wetStack = _stack.GetStack(args.Args.Target, ent.Comp.StatusProto);

        if (wetStack <= 0)
            return;

        var count = ent.Comp.AdditionalHeal;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            count *= stackComp.Stacks;

        args.Args.HealAmount += count;

        _status.TryRemoveStatusEffect(args.Args.Target, ent.Comp.StatusProto);
    }
}
