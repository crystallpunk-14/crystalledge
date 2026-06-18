using Content.Shared._CE.Rarity.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Rarity.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CERarityComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<CERarityPrototype> Rarity = string.Empty;
}
