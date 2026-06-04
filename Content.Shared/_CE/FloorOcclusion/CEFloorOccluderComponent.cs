using Robust.Shared.GameStates;

namespace Content.Shared._CE.FloorOcclusion;

/// <summary>
/// Applies floor occlusion to any <see cref="CEFloorOcclusionComponent"/> that intersects this entity.
/// Unlike the vanilla version, skips entities that are airborne (BodyStatus.InAir).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEFloorOccluderComponent : Component { }
