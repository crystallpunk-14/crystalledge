using Content.Shared._CE.GOAP;
using Robust.Shared.Map;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Partial class for target resolution, tracking, and last-known-position management.
/// </summary>
public sealed partial class CEGOAPSystem
{
    /// <summary>
    /// Reserved target key that always resolves to the entity itself.
    /// </summary>
    public const string SelfTargetKey = "self";

    /// <summary>
    /// Reusable list for expired position keys to avoid allocations during cleanup.
    /// </summary>
    private readonly List<string> _expiredKeys = new();

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
    /// Returns the last-known position for the given key, or null if none is memorized.
    /// </summary>
    public EntityCoordinates? GetLastKnownPosition(Entity<CEGOAPComponent> ent, string key)
    {
        return ent.Comp.LastKnownPositions.TryGetValue(key, out var mem) ? mem.Coordinates : null;
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

        if (target != null)
        {
            var expireAt = _timing.CurTime + ent.Comp.TargetMemoryDuration;
            ent.Comp.LastKnownPositions[key] = new MemorizedPosition(Transform(target.Value).Coordinates, expireAt);
        }

        if (target != old)
        {
            if (old != null)
                RemoveTracker(old.Value, ent.Owner, key);
            if (target != null)
                AddTracker(target.Value, ent.Owner, key);

            var ev = new CETargetChangedEvent(key, target);
            RaiseLocalEvent(ent, ref ev);
        }
    }

    /// <summary>
    /// Removes a last-known position for the given key and notifies sensors.
    /// </summary>
    public void ClearLastKnownPosition(Entity<CEGOAPComponent> ent, string key)
    {
        if (!ent.Comp.LastKnownPositions.Remove(key))
            return;

        var current = ent.Comp.Targets.TryGetValue(key, out var t) ? t : null;
        var ev = new CETargetChangedEvent(key, current);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    /// Removes expired memorized positions and notifies sensors.
    /// Called once per agent per tick from <see cref="UpdateAgent"/>.
    /// </summary>
    private void CleanupExpiredPositions(Entity<CEGOAPComponent> ent)
    {
        var now = _timing.CurTime;

        _expiredKeys.Clear();
        foreach (var (key, mem) in ent.Comp.LastKnownPositions)
        {
            if (now >= mem.ExpireAt)
                _expiredKeys.Add(key);
        }

        foreach (var key in _expiredKeys)
        {
            ClearLastKnownPosition(ent, key);
        }
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
