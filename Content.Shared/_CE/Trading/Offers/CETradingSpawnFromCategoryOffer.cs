using System.Collections.Generic;
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
        var pool = new List<EntProtoId>();
        foreach (var entProto in proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (entProto.Abstract)
                continue;
            if (!entProto.Components.TryGetValue("CETradingCategory", out var entry))
                continue;
            if (entry.Component is not CETradingCategoryComponent cat)
                continue;
            if (!cat.Tags.Overlaps(Tags))
                continue;
            pool.Add(new EntProtoId(entProto.ID));
        }

        if (pool.Count == 0)
            return;

        SelectedEntity = random.Pick(pool);

        if (!proto.TryIndex<EntityPrototype>(SelectedEntity.Value, out var selected))
            return;

        var metaData = entMan.System<MetaDataSystem>();
        metaData.SetEntityName(slotEntity, selected.Name);
        metaData.SetEntityDescription(slotEntity, selected.Description);

        var slot = entMan.GetComponent<CETradingSlotComponent>(slotEntity);
        slot.ActivePreviewProto = SelectedEntity;
    }
}
