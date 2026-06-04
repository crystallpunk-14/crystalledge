using Robust.Shared.GameStates;

namespace Content.Shared._CE.FloorOcclusion;

/// <summary>
/// Receives the floor occlusion (HorizontalCut) shader when intersecting a
/// <see cref="CEFloorOccluderComponent"/> entity, unless the entity is airborne.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CEFloorOcclusionComponent : Component
{
    [ViewVariables]
    public bool Enabled => Colliding.Count > 0;

    [DataField, AutoNetworkedField]
    public List<EntityUid> Colliding = new();
}
