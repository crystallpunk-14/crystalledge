using System.Diagnostics.CodeAnalysis;
using Content.Server._CE.Procedural.Instance.Components;
using Content.Server._CE.Procedural.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.Procedural.Instance;

public sealed partial class CEDungeonInstanceSystem
{
    /// <summary>
    /// Registers a newly generated map as a dungeon instance.
    /// Attaches <see cref="CEDungeonInstanceComponent"/> to the z-network entity if one exists,
    /// otherwise to the map entity itself. Initializes entry point timers.
    /// </summary>
    private void RegisterInstance(EntityUid mapUid, CEDungeonLevelPrototype proto)
    {
        // Determine the anchor entity: z-network entity if the map belongs to one, else the map itself.
        var anchorUid = _zLevels.TryGetZNetwork(mapUid, out var zLevelNetwork) ? zLevelNetwork.Value.Owner : mapUid;

        var instance = EnsureComp<CEDungeonInstanceComponent>(anchorUid);
        instance.PrototypeId = proto.ID;
        instance.Stable = proto.Stable;
        instance.CreatedAt = _timing.CurTime;
        instance.EmptySince = null;

        var mapIds = GetInstanceMapIds(anchorUid);

        // Assign exit targets from the prototype's Exits dictionary.
        if (proto.Exits.Count > 0)
        {
            var query = EntityQueryEnumerator<CEDungeonPassageComponent, TransformComponent>();
            while (query.MoveNext(out _, out var exit, out var xform))
            {
                if (!mapIds.Contains(xform.MapID))
                    continue;

                if (exit.TargetLevel != null)
                    continue;

                if (proto.Exits.TryGetValue(exit.PassageSlot, out var targetLevel))
                    exit.TargetLevel = targetLevel;
            }
        }

        // Initialize entry point deactivation timers (stable levels never expire).
        var dungeonQuery = EntityQueryEnumerator<CEDungeonEntryPointComponent, TransformComponent>();
        while (dungeonQuery.MoveNext(out _, out var entry, out var xform))
        {
            if (!mapIds.Contains(xform.MapID))
                continue;

            if (proto.Stable)
            {
                entry.Stable = true;
            }
            else
            {
                entry.DeactivateAt = _timing.CurTime + entry.ActiveDuration;
            }
        }

        Log.Info($"registered instance '{proto.ID}' on entity {anchorUid} (stable={proto.Stable}).");
    }

    /// <summary>
    /// Returns all <see cref="MapId"/>s belonging to an instance.
    /// Derives them dynamically from the z-network or the anchor's own <see cref="MapComponent"/>.
    /// </summary>
    private HashSet<MapId> GetInstanceMapIds(EntityUid anchorUid)
    {
        var mapIds = new HashSet<MapId>();

        if (_zNetQuery.TryComp(anchorUid, out var zNet))
        {
            foreach (var (_, zMapUid) in zNet.ZLevels)
            {
                if (zMapUid != null && TryComp<MapComponent>(zMapUid.Value, out var mapComp))
                    mapIds.Add(mapComp.MapId);
            }
        }
        else if (TryComp<MapComponent>(anchorUid, out var anchorMap))
        {
            mapIds.Add(anchorMap.MapId);
        }

        return mapIds;
    }

    /// <summary>
    /// Finds an active entry point on any map belonging to the instance.
    /// Returns the entry entity with its component for direct use.
    /// </summary>
    private bool TryFindEnterPoint(CEDungeonLevelPrototype proto, [NotNullWhen(true)] out Entity<CEDungeonEntryPointComponent>? enterPortal)
    {
        enterPortal = null;

        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<CEDungeonEntryPointComponent, TransformComponent>();
        while (query.MoveNext(out var entUid, out var entry, out var xform))
        {
            if (!entry.Active)
                continue;

            if (!entry.Stable && curTime >= entry.DeactivateAt)
            {
                entry.Active = false;
                continue;
            }

            if (xform.MapUid is null)
                continue;

            if (!_zLevels.TryGetZNetwork(xform.MapUid.Value, out var zNetwork))
                continue;

            if (!_instanceQuery.TryComp(zNetwork, out var dungeonInstance))
                continue;

            if (dungeonInstance.PrototypeId != proto)
                continue;

            enterPortal = (entUid, entry);
            return true;
        }

        return false;
    }
}
