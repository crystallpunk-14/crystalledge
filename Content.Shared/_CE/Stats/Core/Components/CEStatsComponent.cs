using Robust.Shared.GameStates;

namespace Content.Shared._CE.Stats.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatsSystem))]
public sealed partial class CEStatsComponent : Component
{
    /// <summary>
    /// Base stat values for this entity (before modifiers).
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<CEStatType, int> BaseStats = new()
    {
        { CEStatType.Vitality, 1 },
        { CEStatType.Strength, 1 }
    };

    /// <summary>
    /// Current actual stat values, taking into account equipment, buffs, etc.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<CEStatType, int> Stats = new()
    {
        { CEStatType.Vitality, 1 },
        { CEStatType.Strength, 1 }
    };
}

public enum CEStatType : byte
{
    Vitality,
    Strength,
}
