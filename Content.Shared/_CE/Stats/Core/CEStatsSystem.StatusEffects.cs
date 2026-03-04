using Content.Shared._CE.Stats.Core.Components;
using Content.Shared._CE.Stats.Core.Prototypes;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.Core;

public sealed partial class CEStatsSystem
{
    private void InitStatusEffects()
    {
        SubscribeLocalEvent<CEStatusEffectModifyStatsComponent, StatusEffectRelayedEvent<CECalculateStatEvent>>(OnCalculateStatusEffectStat);

        SubscribeLocalEvent<CEStatusEffectModifyStatsComponent, StatusEffectAppliedEvent>(OnAppliedEffectApplied);
        SubscribeLocalEvent<CEStatusEffectModifyStatsComponent, StatusEffectRemovedEvent>(OnAppliedEffectRemoved);
        SubscribeLocalEvent<CEStatusEffectModifyStatsComponent, CEStatusEffectStackEditedEvent>(OnStackEdit);
    }

    private void OnCalculateStatusEffectStat(Entity<CEStatusEffectModifyStatsComponent> ent, ref StatusEffectRelayedEvent<CECalculateStatEvent> args)
    {
        var stack = 1;

        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stack = stackComp.Stack;

        args.Args.AffectValue(ent.Comp.ModifyStats.GetValueOrDefault(args.Args.StatType, 0) * stack);

        for (int i = 0; i < stack; i++)
        {
            args.Args.AffectMultiplier(ent.Comp.MultiplyStats.GetValueOrDefault(args.Args.StatType, 1f));
        }
    }

    private void OnAppliedEffectApplied(Entity<CEStatusEffectModifyStatsComponent> ent, ref StatusEffectAppliedEvent args)
    {
        UpdateEffectStats(ent, args.Target);
    }

    private void OnAppliedEffectRemoved(Entity<CEStatusEffectModifyStatsComponent> ent, ref StatusEffectRemovedEvent args)
    {
        UpdateEffectStats(ent, args.Target);
    }

    private void OnStackEdit(Entity<CEStatusEffectModifyStatsComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        UpdateEffectStats(ent, args.Target);
    }

    private void UpdateEffectStats(Entity<CEStatusEffectModifyStatsComponent> ent, EntityUid owner)
    {
        HashSet<ProtoId<CECharacterStatPrototype>> updatedStats = new();

        foreach (var stat in ent.Comp.ModifyStats)
        {
            updatedStats.Add(stat.Key);
        }

        foreach (var stat in ent.Comp.MultiplyStats)
        {
            updatedStats.Add(stat.Key);
        }

        foreach (var stat in updatedStats)
        {
            RecalculateStat(owner, stat);
        }
    }
}
