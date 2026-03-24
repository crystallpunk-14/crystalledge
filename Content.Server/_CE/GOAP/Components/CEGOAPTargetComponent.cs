namespace Content.Server._CE.GOAP.Components;

/// <summary>
/// Added to entities that are currently selected as a GOAP target.
/// Used fo event-based target sensors update (checking MobStateChanged for targets, etc).
/// Tracks which GOAP agents reference this entity and under which target keys.
/// Automatically managed by <see cref="CEGOAPSystem.SetTarget"/>.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPTargetComponent : Component
{
    /// <summary>
    /// Maps GOAP entity UID → set of target keys that reference this entity.
    /// </summary>
    public Dictionary<EntityUid, HashSet<string>> Trackers = new();
}
