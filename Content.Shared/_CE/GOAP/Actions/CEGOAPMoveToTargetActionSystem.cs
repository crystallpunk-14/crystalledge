namespace Content.Shared._CE.GOAP.Actions;

/// <summary>
/// Moves the NPC towards its current target entity.
/// Uses absolute grid coordinates for proper pathfinding (avoiding space tiles).
/// Only re-registers steering when the target moves significantly.
/// </summary>
public sealed partial class CEGOAPMoveToTargetAction : CEGOAPActionBase<CEGOAPMoveToTargetAction>
{
    /// <summary>
    /// How close the NPC needs to get to the target to consider the action complete.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// How far the target must move before re-registering the steering destination.
    /// Prevents constant pathfinding recalculation while still tracking moving targets.
    /// </summary>
    [DataField]
    public float ReregisterThreshold = 1.5f;
}
