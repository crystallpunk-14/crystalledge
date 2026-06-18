using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Trading;

[ImplicitDataDefinitionForInheritors, MeansImplicitUse]
[Serializable, NetSerializable]
public abstract partial class CETradingOffer
{
    // Returns the entity placed in the player's hand, or null for service offers with no item.
    public abstract EntityUid? Effect(CETradingOfferArgs args);

    public abstract void UpdateSlotVisuals(
        EntityUid slotEntity,
        IEntityManager entMan,
        IPrototypeManager proto,
        IRobustRandom random);
}

public record struct CETradingOfferArgs(
    IEntityManager EntityManager,
    EntityUid Trader,
    EntityCoordinates Position);
