using Robust.Shared.GameStates;

namespace Content.Shared._CE.Stats.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(CEStatsSystem))]
public sealed partial class CEStatsComponent : Component
{
    /// <summary>
    /// Basic vitality level of this entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BaseVitality = 1;

    /// <summary>
    /// Current actual vitality level of this entity, taking into account equipment, buffs, etc.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Vitality = 1;
}

public enum CEStatType
{
    Vitality,
    Strength,
}
