using Content.Shared._CE.Stats.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.Core.Components;

/// <summary>
/// Allows clothing items to modify character stats when equipped.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatsSystem))]
public sealed partial class CEClothingModifyStatsComponent : Component
{
    /// <summary>
    /// Flat additive modifiers to character stats when this clothing is equipped.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<CECharacterStatPrototype>, int> ModifyStats = new();

    /// <summary>
    /// Multiplicative modifiers to character stats when this clothing is equipped.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<CECharacterStatPrototype>, float> MultiplyStats = new();
}
