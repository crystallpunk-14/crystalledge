using Content.Shared._CE.Fire;
using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared._CE.Water;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.Skill.Skills.HealingWaters;

public sealed partial class CEHealingWatersStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CESharedWaterSystem _water = default!;
    [Dependency] private readonly CEFireSystem _fire = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEHealingWatersStatusEffectComponent, StatusEffectRelayedEvent<CEGetHealAmountEvent>>(OnGetHealAmount);
    }

    private void OnGetHealAmount(Entity<CEHealingWatersStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEGetHealAmountEvent> args)
    {
        if (!TryComp<CEWettableComponent>(args.Args.Target, out var wettableComp))
            return;

        if (_water.GetWettableStacks((args.Args.Target, wettableComp)) <= 0)
            return;

        var count = ent.Comp.AdditionalHeal;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            count *= stackComp.Stacks;

        args.Args.HealAmount += count;

        _status.TryRemoveStatusEffect(args.Args.Target, wettableComp.StatusEffect);
        _fire.SpawnSteamEffect(args.Args.Target);
    }
}
