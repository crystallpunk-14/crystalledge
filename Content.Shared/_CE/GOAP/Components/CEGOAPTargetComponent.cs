using Robust.Shared.GameStates;

namespace Content.Shared._CE.GOAP.Components;

/// <summary>
/// Added to entities that are currently selected as a GOAP target.
/// Used for event-based target sensors update (checking MobStateChanged for targets, etc).
/// Tracks which GOAP agents reference this entity and under which target keys.
/// Automatically managed by CEGOAPSystem.SetTarget.
/// Networked so clients can detect when the local player is being hunted.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEGOAPTargetComponent : Component
{
    /// <summary>
    /// Maps GOAP entity UID -> set of target keys that reference this entity.
    /// Server-side only — not networked (clients only need component existence).
    /// </summary>
    [NonSerialized]
    public Dictionary<EntityUid, HashSet<string>> Trackers = new();
}
