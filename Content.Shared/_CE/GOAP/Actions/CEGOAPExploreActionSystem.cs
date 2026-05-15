namespace Content.Shared._CE.GOAP.Actions;

/// <summary>
/// Picks a random walkable tile within radius and walks to it.
/// Used as a low-priority idle behavior so mobs circulate around their area.
/// </summary>
public sealed partial class CEGOAPExploreAction : CEGOAPActionBase<CEGOAPExploreAction>
{
    /// <summary>
    /// Maximum distance to pick a destination (in tiles).
    /// </summary>
    [DataField]
    public float ExploreRadius = 8f;

    /// <summary>
    /// Number of random directions to sample when looking for a valid destination.
    /// </summary>
    [DataField]
    public int SampleDirections = 12;
}

//Server only realization
