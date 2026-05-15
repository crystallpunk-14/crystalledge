namespace Content.Shared._CE.GOAP.Actions;

/// <summary>
/// Flee away from its current target using BFS over pathfinding polygons.
/// Every recalculation interval, performs a BFS from the NPC's current tile
/// and picks the reachable tile farthest from the threat.
/// </summary>
public sealed partial class CEGOAPFleeAction : CEGOAPActionBase<CEGOAPFleeAction>
{
    /// <summary>
    /// Maximum BFS iterations (depth) when searching for flee destinations.
    /// </summary>
    [DataField]
    public int MaxBfsIterations = 10;

    /// <summary>
    /// How often to recalculate the flee destination, in seconds.
    /// </summary>
    [DataField]
    public float RecalculateInterval = 1f;
}

// Server only realization
