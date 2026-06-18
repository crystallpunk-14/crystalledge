using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._CE.Trading;

namespace Content.Shared._CE.Trading.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CETradingSlotComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Price;

    [DataField, AutoNetworkedField]
    public Vector2 Offset = new(0, 1f);

    [DataField, AutoNetworkedField]
    public EntProtoId? ActivePreviewProto;

    [DataField]
    public List<CETradingOffer> Offers = new();

    [DataField]
    public CETradingOffer? ActiveOffer;
}
