using System.Collections.Generic;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.Trading.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CE.Trading.Offers;

public sealed partial class CETradingApplyEffectOffer : CETradingOffer
{
    [DataField]
    public string OfferName = string.Empty;

    [DataField]
    public string OfferDescription = string.Empty;

    [DataField]
    public EntProtoId? DummyIcon;

    [DataField]
    public List<CEEntityEffect> EffectsToApply = new();

    public override EntityUid? Effect(CETradingOfferArgs args)
    {
        var effectArgs = new CEEntityEffectArgs(
            args.EntityManager,
            args.Trader,
            null,
            Angle.Zero,
            0f,
            args.Trader,
            args.Position);

        foreach (var effect in EffectsToApply)
            effect.Effect(effectArgs);

        return null;
    }

    public override void UpdateSlotVisuals(EntityUid slotEntity, IEntityManager entMan, IPrototypeManager proto, IRobustRandom random)
    {
        var metaData = entMan.System<MetaDataSystem>();
        metaData.SetEntityName(slotEntity, OfferName);
        metaData.SetEntityDescription(slotEntity, OfferDescription);

        var slot = entMan.GetComponent<CETradingSlotComponent>(slotEntity);
        slot.ActivePreviewProto = DummyIcon;
    }
}
