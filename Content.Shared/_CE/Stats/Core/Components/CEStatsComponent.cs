using Content.Shared._CE.Stats.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.Core.Components;

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
