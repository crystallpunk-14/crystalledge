using Content.Server._CE.Procedural.Instance.Components;
using Content.Server._CE.Procedural.Prototypes;
using Content.Shared._CE.ZLevels.Core.Components;
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
    private EntityUid RegisterInstance(EntityUid mapUid, CEDungeonLevelPrototype proto)
    {
        // Determine the anchor entity: z-network entity if the map belongs to one, else the map itself.
        var anchorUid = FindZNetworkForMap(mapUid) ?? mapUid;

        var instance = EnsureComp<CEDungeonInstanceComponent>(anchorUid);
        instance.PrototypeId = proto.ID;
        instance.Stable = proto.Stable;
        instance.CreatedAt = _timing.CurTime;
        instance.EmptySince = null;

        var mapIds = GetInstanceMapIds(anchorUid);

        // Assign exit targets from the prototype's Exits dictionary.
        ConfigureExitTargets(mapIds, proto);

        // Initialize entry point deactivation timers.
        InitializeEntryTimers(mapIds);

        Log.Info($"CEDungeonInstanceSystem: registered instance '{proto.ID}' on entity {anchorUid} (stable={proto.Stable}).");
        return anchorUid;
    }

    /// <summary>
    /// Assigns <see cref="CEDungeonLevelExitComponent.TargetLevel"/> to exit entities whose
    /// <see cref="CEDungeonLevelExitComponent.ExitSlot"/> matches a key in the prototype's Exits dictionary,
    /// but only if the exit doesn't already have a target level set.
    /// </summary>
    private void ConfigureExitTargets(HashSet<MapId> mapIds, CEDungeonLevelPrototype proto)
    {
        if (proto.Exits.Count == 0)
            return;

        var query = EntityQueryEnumerator<CEDungeonLevelExitComponent, TransformComponent>();
        while (query.MoveNext(out _, out var exit, out var xform))
        {
            if (!mapIds.Contains(xform.MapID))
                continue;

            if (exit.TargetLevel != null)
                continue;

            if (proto.Exits.TryGetValue(exit.ExitSlot, out var targetLevel))
                exit.TargetLevel = targetLevel;
        }
    }

    /// <summary>
    /// Sets <see cref="CEDungeonLevelEntryComponent.DeactivateAt"/> for all entries on the given maps.
    /// </summary>
    private void InitializeEntryTimers(HashSet<MapId> mapIds)
    {
        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<CEDungeonLevelEntryComponent, TransformComponent>();
        while (query.MoveNext(out _, out var entry, out var xform))
        {
            if (!mapIds.Contains(xform.MapID))
                continue;

            entry.DeactivateAt = curTime + entry.ActiveDuration;
        }
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
    /// Finds the z-network entity that contains the given map entity, if any.
    /// </summary>
    private EntityUid? FindZNetworkForMap(EntityUid mapUid)
    {
        var query = EntityQueryEnumerator<CEZLevelsNetworkComponent>();
        while (query.MoveNext(out var netUid, out var zNet))
        {
            foreach (var (_, zMapUid) in zNet.ZLevels)
            {
                if (zMapUid == mapUid)
                    return netUid;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds an existing instance of the given prototype that has at least one active entry.
    /// For stable prototypes, returns any existing instance regardless of entry state.
    /// </summary>
    private EntityUid? FindInstanceWithActiveEntry(CEDungeonLevelPrototype proto)
    {
        var query = EntityQueryEnumerator<CEDungeonInstanceComponent>();
        while (query.MoveNext(out var uid, out var inst))
        {
            if (inst.PrototypeId != proto.ID)
                continue;

            if (proto.Stable)
                return uid;

            if (FindActiveEntry(uid) != null)
                return uid;
        }

        return null;
    }

    /// <summary>
    /// Finds an active entry point on any map belonging to the instance.
    /// Returns the entry entity with its component for direct use.
    /// </summary>
    private Entity<CEDungeonLevelEntryComponent>? FindActiveEntry(EntityUid instanceUid)
    {
        if (!_instanceQuery.TryComp(instanceUid, out _))
            return null;

        var curTime = _timing.CurTime;
        var mapIds = GetInstanceMapIds(instanceUid);

        var query = EntityQueryEnumerator<CEDungeonLevelEntryComponent, TransformComponent>();
        while (query.MoveNext(out var entUid, out var entry, out var xform))
        {
            if (!mapIds.Contains(xform.MapID))
                continue;

            if (!entry.Active)
                continue;

            if (curTime >= entry.DeactivateAt)
            {
                entry.Active = false;
                continue;
            }

            return (entUid, entry);
        }

        return null;
    }

    /// <summary>
    /// Deletes an instance: delegates to <see cref="CEZLevelsSystem.DeleteZNetwork"/> for z-networks,
    /// or directly deletes a standalone map entity.
    /// </summary>
    private void DeleteInstance(EntityUid anchorUid)
    {
        if (_zNetQuery.HasComp(anchorUid))
        {
            _zLevels.DeleteZNetwork(anchorUid);
        }
        else
        {
            QueueDel(anchorUid);
        }
    }
}
