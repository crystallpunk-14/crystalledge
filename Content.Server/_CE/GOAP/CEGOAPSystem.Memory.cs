using Content.Shared._CE.GOAP;
using Robust.Shared.Map;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Partial class for last-known-position management.
/// </summary>
public sealed partial class CEGOAPSystem
{
    public void SetLastKnownPosition(Entity<CEGOAPComponent> ent, string key, EntityCoordinates coords)
    {
        ent.Comp.LastKnownPositions[key] = coords;
    }

    /// <summary>
    /// Returns the last-known position for the given key, or null if none is memorized.
    /// </summary>
    public EntityCoordinates? GetLastKnownPosition(Entity<CEGOAPComponent> ent, string key)
    {
        return ent.Comp.LastKnownPositions.TryGetValue(key, out var mem) ? mem : null;
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
}
