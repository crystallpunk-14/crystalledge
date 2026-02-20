using Content.Shared._CE.Stats.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.Core.Components;

/// <summary>
/// Manages character stats for an entity. Stores both base values and calculated values after applying modifiers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatsSystem))]
public sealed partial class CEStatsComponent : Component
{
    /// <summary>
    /// Base stat values for this entity (before modifiers).
    /// </summary>
    [DataField, AutoNetworkedField, AlwaysPushInheritance]
    public Dictionary<ProtoId<CECharacterStatPrototype>, int> BaseStats = new();

    /// <summary>
    /// Current actual stat values, taking into account equipment, buffs, etc.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<CECharacterStatPrototype>, int> Stats = new();
}
