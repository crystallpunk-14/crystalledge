using Robust.Shared.GameStates;

namespace Content.Shared._CE.BossHealthBar;

/// <summary>
/// Marker component that causes a boss health bar UI widget to appear
/// for nearby clients when this entity is in their PVS range.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEBossHealthBarComponent : Component
{
}
