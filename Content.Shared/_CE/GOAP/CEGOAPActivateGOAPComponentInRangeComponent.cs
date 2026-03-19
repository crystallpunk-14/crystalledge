using Robust.Shared.GameStates;

namespace Content.Shared._CE.GOAP;

/// <summary>
/// Activates all GOAP for mobs in range.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEGOAPActivateGOAPComponentInRangeComponent : Component
{
    /// <summary>
    /// Range for check.
    /// </summary>
    [DataField]
    public float Range = 10f;
}
