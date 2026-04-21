using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Tag;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CETagSystem))]
public sealed partial class CETagComponent : Component
{
    [DataField, ViewVariables, AutoNetworkedField]
    public HashSet<ProtoId<CETagPrototype>> Tags = new();
}
