using Robust.Shared.GameStates;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Stores accumulated damage for an entity as a single integer.
/// Damage starts at 0 and increases when the entity is hurt.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CESharedDamageableSystem))]
public sealed partial class CEDamageableComponent : Component
{
    [DataField, AutoNetworkedField]
    public int TotalDamage;
}
