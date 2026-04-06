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

    /// <summary>
    /// Previous <see cref="TotalDamage"/> before the last state update.
    /// Used on the client to compute the real damage delta when state syncs from the server.
    /// Not networked — purely local tracking.
    /// </summary>
    [ViewVariables]
    public int PreviousTotalDamage;
}
