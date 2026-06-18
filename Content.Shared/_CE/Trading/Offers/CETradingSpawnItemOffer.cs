using Content.Shared._CE.Trading.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CE.Trading.Offers;

public sealed partial class CETradingSpawnItemOffer : CETradingOffer
{
    [DataField(required: true)]
    public EntProtoId Entity;

    public override EntityUid? Effect(CETradingOfferArgs args)
    {
        var spawned = args.EntityManager.SpawnEntity(Entity, args.Position);
        var hands = args.EntityManager.System<SharedHandsSystem>();
        // animate: false — server handles the animation itself so the buyer sees it.
        hands.TryPickupAnyHand(args.Trader, spawned, checkActionBlocker: false, animate: false);
        return spawned;
    }

    public override void UpdateSlotVisuals(EntityUid slotEntity, IEntityManager entMan, IPrototypeManager proto, IRobustRandom random)
    {
        if (!proto.TryIndex<EntityPrototype>(Entity, out var entProto))
            return;

        var metaData = entMan.System<MetaDataSystem>();
        metaData.SetEntityName(slotEntity, entProto.Name);
        metaData.SetEntityDescription(slotEntity, entProto.Description);

        var slot = entMan.GetComponent<CETradingSlotComponent>(slotEntity);
        slot.ActivePreviewProto = Entity;
    }
}
