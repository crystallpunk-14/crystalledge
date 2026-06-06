namespace Content.Server._CE.ZLevels.Pathfinding;

/// <summary>
/// Tracks the cross-Z pathfinding portals created for ramps anchored to this grid.
/// Lives on the grid that owns the ramps (the lower map of each portal pair).
/// </summary>
[RegisterComponent]
public sealed partial class CEZPortalComponent : Component
{
    /// <summary>
    /// Per-ramp portal data, keyed by the ramp entity.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, CEZRampPortal> Ramps = new();
}

/// <summary>
/// A single ramp's portal: its pathfinder handle plus the geometry the steering seam needs.
/// </summary>
public struct CEZRampPortal
{
    /// <summary>Handle returned by PathfindingSystem.TryCreatePortal, used to remove it.</summary>
    public int Handle;

    /// <summary>Tile the ramp occupies on this (lower) grid.</summary>
    public Vector2i RampTile;

    /// <summary>The direction walked to ascend the ramp (opposite of the ramp's facing).</summary>
    public Direction UphillDir;
}
