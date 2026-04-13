using Robust.Shared.GameStates;

namespace Content.Shared._CE.Water;

/// <summary>
/// Marks an entity as a water tile. Flowing water pushes entities via conveyor mechanics.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEWaterComponent : Component
{
    /// <summary>
    /// Whether this water is flowing and should push entities.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Flowing;
}
