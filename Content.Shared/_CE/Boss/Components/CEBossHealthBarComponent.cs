using Robust.Shared.GameStates;

namespace Content.Shared._CE.Boss;

/// <summary>
/// Causes a boss health bar UI widget to appear for nearby clients when this entity is in their PVS range.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEBossHealthBarComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;
}
