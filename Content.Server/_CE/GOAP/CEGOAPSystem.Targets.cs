using Content.Server._CE.GOAP.Components;
using Content.Shared._CE.GOAP;
using Robust.Shared.Map;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Partial class for target resolution, tracking.
/// </summary>
public sealed partial class CEGOAPSystem
{
    /// <summary>
    /// Reserved target key that always resolves to the entity itself.
    /// </summary>
    public const string SelfTargetKey = "self";

    /// <summary>
    /// Resolves a target key to an entity. "self" returns the owner, null returns null.
    /// </summary>
    public EntityUid? GetTarget(Entity<CEGOAPComponent> ent, string? targetKey)
    {
        if (targetKey == null)
            return null;

        if (targetKey == SelfTargetKey)
            return ent.Owner;

        return ent.Comp.Targets.TryGetValue(targetKey, out var target) ? target : null;
    }

    /// <summary>
    /// Writes a target entity into the Targets dictionary and auto-tracks its position.
    /// When target is non-null, LastKnownPositions[key] is updated with a fresh expiry.
    /// When target becomes null, the existing memorized position is preserved until it expires.
    /// Raises <see cref="CETargetChangedEvent"/> when the resolved target changes.
    /// </summary>
    public void SetTarget(Entity<CEGOAPComponent> ent, string key, EntityUid? target)
    {
        var old = ent.Comp.Targets.TryGetValue(key, out var prev) ? prev : null;
        ent.Comp.Targets[key] = target;

        if (target == old)
            return;

        if (old != null)
            RemoveTracker(old.Value, ent.Owner, key);

        //We lost target
        if (target is null)
        {
            //If new target is null, but old is not, we remembrer last known position
            if (old != null)
                SetLastKnownPosition(ent, key, Transform(old.Value).Coordinates);
        }
        // We set new target
        else
        {
            ClearLastKnownPosition(ent, key); //We dont need remember last known position - WE SEE TARGET RIGHT NOW
            AddTracker(target.Value, ent.Owner, key);
        }

        var ev = new CETargetChangedEvent(key, target);
        RaiseLocalEvent(ent, ref ev);

        // Defer replanning to the next UpdateAgent tick so all sensors
        // finish updating WorldState before the planner reads it.
        // Reset ActiveGoalIndex so Replan doesn't keep a stale plan.
        ent.Comp.ActiveGoalIndex = -1;
        ent.Comp.NextPlanTime = TimeSpan.Zero;
    }

    private void AddTracker(EntityUid target, EntityUid goapOwner, string key)
    {
        var comp = EnsureComp<CEGOAPTargetComponent>(target);
        if (!comp.Trackers.TryGetValue(goapOwner, out var keys))
        {
            keys = new HashSet<string>();
            comp.Trackers[goapOwner] = keys;
        }
        keys.Add(key);
    }

    private void RemoveTracker(EntityUid target, EntityUid goapOwner, string key)
    {
        if (!TryComp<CEGOAPTargetComponent>(target, out var comp))
            return;

        if (!comp.Trackers.TryGetValue(goapOwner, out var keys))
            return;

        keys.Remove(key);
        if (keys.Count == 0)
            comp.Trackers.Remove(goapOwner);
        if (comp.Trackers.Count == 0)
            RemCompDeferred<CEGOAPTargetComponent>(target);
    }

    /// <summary>
    /// Cleans up all target trackers when a GOAP entity is destroyed.
    /// </summary>
    private void CleanupTrackers(Entity<CEGOAPComponent> ent)
    {
        foreach (var (key, target) in ent.Comp.Targets)
        {
            if (target != null)
                RemoveTracker(target.Value, ent.Owner, key);
        }
    }
}

/// <summary>
/// Raised on the GOAP entity when a target in the Targets dictionary changes.
/// </summary>
[ByRefEvent]
public record struct CETargetChangedEvent(string TargetKey, EntityUid? NewTarget);
