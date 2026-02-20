using Content.Shared._CE.Stats.Core.Components;
using Content.Shared._CE.Stats.Core.Prototypes;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.Core;

public sealed partial class CEStatsSystem
{
    private void InitClothing()
    {
        SubscribeLocalEvent<CEClothingModifyStatsComponent, InventoryRelayedEvent<CECalculateStatEvent>>(OnCalculateStat);

        SubscribeLocalEvent<CEClothingModifyStatsComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CEClothingModifyStatsComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnCalculateStat(Entity<CEClothingModifyStatsComponent> ent, ref InventoryRelayedEvent<CECalculateStatEvent> args)
    {
        args.Args.AffectValue(ent.Comp.ModifyStats.GetValueOrDefault(args.Args.StatType, 0));
        args.Args.AffectMultiplier(ent.Comp.MultiplyStats.GetValueOrDefault(args.Args.StatType, 1f));
    }

    private void OnEquipped(Entity<CEClothingModifyStatsComponent> ent, ref ClothingGotEquippedEvent args)
    {
        UpdateClothingStats(ent, args.Wearer);
    }

    private void OnUnequipped(Entity<CEClothingModifyStatsComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        UpdateClothingStats(ent, args.Wearer);
    }

    private void UpdateClothingStats(Entity<CEClothingModifyStatsComponent> ent, EntityUid wearer)
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
            RecalculateStat(wearer, stat);
        }
    }
}
