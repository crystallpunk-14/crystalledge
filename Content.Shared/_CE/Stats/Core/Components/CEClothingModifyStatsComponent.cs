using Robust.Shared.GameStates;

namespace Content.Shared._CE.Stats.Core.Components;

/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatsSystem))]
public sealed partial class CEClothingModifyStatsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<CEStatType, int> ModifyStats = new();

    [DataField, AutoNetworkedField]
    public Dictionary<CEStatType, float> MultiplyStats = new();
}
