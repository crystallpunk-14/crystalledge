using Robust.Shared.GameStates;

namespace Content.Shared._CE.Frost;

/// <summary>
/// Marker component for ice tile entities, used for ECS-based detection.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEIceComponent : Component
{
}
