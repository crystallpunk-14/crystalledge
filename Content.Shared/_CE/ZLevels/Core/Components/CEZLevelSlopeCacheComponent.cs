using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Cached positions of slope entities (HighGround that act as ramps) on a given map grid.
/// Maintained by <see cref="Content.Server._CE.ZLevels.CEZLevelSlopeCacheSystem"/>.
/// Stored on the map/grid entity for quick spatial lookups by NPC navigation systems.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEZLevelSlopeCacheComponent : Component
{
    /// <summary>
    /// All ramp-like slope positions on this map, keyed by tile index.
    /// Value contains the slope entity UID and the cardinal direction it faces (the "uphill" direction).
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector2i, CECachedSlope> Slopes = new();
}

/// <summary>
/// Cached data about a single slope entity for NPC pathfinding.
/// </summary>
public struct CECachedSlope
{
    /// <summary>
    /// The slope entity UID.
    /// </summary>
    public EntityUid Entity;

    /// <summary>
    /// The cardinal direction the slope faces — i.e. the direction you walk to ascend.
    /// </summary>
    public Direction Direction;
}
