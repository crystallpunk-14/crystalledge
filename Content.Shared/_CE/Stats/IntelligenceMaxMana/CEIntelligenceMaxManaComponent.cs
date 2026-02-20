using Content.Shared._CE.Stats.Core;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Stats.IntelligenceMaxMana;

/// <summary>
/// Links the Intelligence stat to the maximum mana stored in a magic energy container.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatsSystem))]
public sealed partial class CEIntelligenceMaxManaComponent : Component
{
    /// <summary>
    /// Additional mana granted per point of intelligence.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ManaPerIntelligence = 4;
}
