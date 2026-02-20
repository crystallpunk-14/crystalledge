using Content.Shared._CE.Stats.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.Core.Components;

/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatsSystem))]
public sealed partial class CEClothingModifyStatsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<CECharacterStatPrototype>, int> ModifyStats = new();

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<CECharacterStatPrototype>, float> MultiplyStats = new();
}
