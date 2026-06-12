using Content.Shared._CE.Loot;
using Content.Shared._CE.Tag;
using Content.Shared._CE.Trading.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CE.Trading.Offers;

public sealed partial class CETradingSpawnFromCategoryOffer : CETradingOffer
{
    [DataField]
    public HashSet<ProtoId<CETagPrototype>> Tags = new();

    // DataField so the selection persists across server restarts.
    [DataField]
    public EntProtoId? SelectedEntity;

    public override EntityUid? Effect(CETradingOfferArgs args)
    {
        if (SelectedEntity == null)
            return null;

        var spawned = args.EntityManager.SpawnEntity(SelectedEntity.Value, args.Position);
        var hands = args.EntityManager.System<SharedHandsSystem>();
        // animate: false — server handles the animation itself so the buyer sees it.
        hands.TryPickupAnyHand(args.Trader, spawned, checkActionBlocker: false, animate: false);
        return spawned;
    }

    public override void UpdateSlotVisuals(EntityUid slotEntity, IEntityManager entMan, IPrototypeManager proto, IRobustRandom random)
    {
        var pool = new List<(EntProtoId Id, float Weight)>();

        foreach (var entProto in proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (entProto.Abstract)
                continue;
            if (!entProto.Components.TryGetValue("CELootCategory", out var compEntry))
                continue;
            if (compEntry.Component is not CELootCategoryComponent cat)
                continue;

            var weight = 0f;
            foreach (var tagEntry in cat.Tags)
            {
                if (Tags.Contains(tagEntry.Tag))
                    weight += tagEntry.Weight;
            }

            if (weight <= 0f)
                continue;

            pool.Add((new EntProtoId(entProto.ID), weight));
        }

        if (pool.Count == 0)
            return;

        var totalWeight = 0f;
        foreach (var (_, w) in pool)
            totalWeight += w;

        var roll = random.NextFloat() * totalWeight;
        var cumulative = 0f;
        var picked = pool[^1].Id;
        foreach (var (id, w) in pool)
        {
            cumulative += w;
            if (roll <= cumulative)
            {
                picked = id;
                break;
            }
        }

        SelectedEntity = picked;

        if (!proto.TryIndex<EntityPrototype>(SelectedEntity.Value, out var selected))
            return;

        var metaData = entMan.System<MetaDataSystem>();
        metaData.SetEntityName(slotEntity, selected.Name);
        metaData.SetEntityDescription(slotEntity, selected.Description);

        var slot = entMan.GetComponent<CETradingSlotComponent>(slotEntity);
        slot.ActivePreviewProto = SelectedEntity;
    }
}
