using Content.Shared._CE.Stats.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.Core.Components;

/// <summary>
/// Allows a status effect to modify character stats while it is applied.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatsSystem))]
public sealed partial class CEStatusEffectModifyStatsComponent : Component
{
    /// <summary>
    /// Flat additive modifiers to character stats while this status effect is applied.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<CECharacterStatPrototype>, int> ModifyStats = new();

    /// <summary>
    /// Multiplicative modifiers to character stats while this status effect is applied.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<CECharacterStatPrototype>, float> MultiplyStats = new();
}
