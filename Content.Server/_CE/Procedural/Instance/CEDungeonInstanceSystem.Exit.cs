using System.Linq;
using Content.Server._CE.Procedural.Instance.Components;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Procedural;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.Map;

namespace Content.Server._CE.Procedural.Instance;

public sealed partial class CEDungeonInstanceSystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    private void InitializeExit()
    {
        SubscribeLocalEvent<CEDungeonLevelExitComponent, ActivateInWorldEvent>(OnExitActivated);
        SubscribeLocalEvent<CEDungeonLevelExitComponent, CEDungeonExitDoAfterEvent>(OnExitDoAfterComplete);
    }

    /// <summary>
    /// Player activates an exit portal:
    /// 1) Immediately determine or start generating the target instance.
    /// 2) Start a DoAfter (minimum wait time so players can't tell if it's a new or existing instance).
    /// </summary>
    private void OnExitActivated(Entity<CEDungeonLevelExitComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        // Don't allow multiple activations on the same exit while one is pending.
        if (_pendingGenerations.ContainsKey(ent.Owner))
            return;

        args.Handled = true;

        var exit = ent.Comp;

        if (!_proto.TryIndex(exit.TargetLevel, out var proto))
        {
            Log.Error($"CEDungeonInstanceSystem: unknown level prototype '{exit.TargetLevel}'.");
            return;
        }

        // Check if an existing instance with an active entry is available.
        var existingInstance = FindInstanceWithActiveEntry(proto);

        if (existingInstance == null)
        {
            // No existing instance — start generation immediately (runs in background).
            _pendingGenerations[ent.Owner] = _dungeon.GenerateLevelAsync(proto);
        }

        // Start the DoAfter (minimum transition time).
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            TimeSpan.FromSeconds(exit.TransitionDuration),
            new CEDungeonExitDoAfterEvent(),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// DoAfter completed — gather nearby players and teleport them to the instance.
    /// Delegates to an async helper since event handlers can't be async (ref params).
    /// </summary>
    private void OnExitDoAfterComplete(Entity<CEDungeonLevelExitComponent> ent, ref CEDungeonExitDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            _pendingGenerations.Remove(ent.Owner);
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;

        HandleExitDoAfterAsync(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Async continuation for exit DoAfter — resolves the target instance and teleports players.
    /// </summary>
    private async void HandleExitDoAfterAsync(EntityUid exitUid, CEDungeonLevelExitComponent exit)
    {
        if (!_proto.TryIndex(exit.TargetLevel, out var proto))
        {
            _pendingGenerations.Remove(exitUid);
            return;
        }

        EntityUid? instanceUid;

        if (_pendingGenerations.TryGetValue(exitUid, out var genTask))
        {
            _pendingGenerations.Remove(exitUid);

            var result = await genTask;

            if (!result.Success || result.MapUid == null || result.MapId == null)
            {
                Log.Error($"CEDungeonInstanceSystem: generation failed for '{exit.TargetLevel}'.");
                return;
            }

            instanceUid = RegisterInstance(result.MapUid.Value, proto);
        }
        else
        {
            instanceUid = FindInstanceWithActiveEntry(proto);

            if (instanceUid == null)
            {
                var result = await _dungeon.GenerateLevelAsync(proto);
                if (!result.Success || result.MapUid == null || result.MapId == null)
                {
                    Log.Error($"CEDungeonInstanceSystem: fallback generation failed for '{exit.TargetLevel}'.");
                    return;
                }

                instanceUid = RegisterInstance(result.MapUid.Value, proto);
            }
        }

        if (instanceUid == null || !_instanceQuery.TryComp(instanceUid.Value, out var inst))
            return;

        var candidates = GatherNearbyPlayers(exitUid, exit.SearchRadius, exit.Throughput);

        if (candidates.Count == 0)
        {
            Log.Warning("CEDungeonInstanceSystem: no players found near exit for transition.");
            return;
        }

        // Find an active entry and teleport.
        TeleportGroupToInstance(instanceUid.Value, inst, candidates);
    }

    /// <summary>
    /// Gathers player entities near the exit, limited by throughput.
    /// Uses the generic <see cref="EntityLookupSystem.GetEntitiesInRange{T}"/> overload.
    /// </summary>
    private List<EntityUid> GatherNearbyPlayers(EntityUid exitUid, float radius, int maxCount)
    {
        var nearby = _lookup.GetEntitiesInRange<CEMobStateComponent>(_transform.GetMapCoordinates(exitUid), radius);
        var candidates = nearby.Select(e => e.Owner).ToList();

        if (candidates.Count > maxCount)
        {
            _random.Shuffle(candidates);
            candidates = candidates.Take(maxCount).ToList();
        }

        return candidates;
    }

    /// <summary>
    /// Teleports a group of players to an active entry in the target instance.
    /// </summary>
    private void TeleportGroupToInstance(
        EntityUid instanceUid,
        CEDungeonInstanceComponent inst,
        List<EntityUid> group)
    {
        var targetEntry = FindActiveEntry(instanceUid);

        MapCoordinates targetCoords;
        if (targetEntry != null)
        {
            targetCoords = _transform.GetMapCoordinates(targetEntry.Value);

            if (_entryQuery.TryComp(targetEntry.Value, out var entryComp))
                entryComp.Active = false;
        }
        else
        {
            // Fallback: use the first map in the instance at its origin.
            var mapIds = GetInstanceMapIds(instanceUid);
            if (mapIds.Count > 0)
            {
                targetCoords = new MapCoordinates(default, mapIds.First());
            }
            else
            {
                Log.Error($"CEDungeonInstanceSystem: no maps found for instance '{inst.PrototypeId}'.");
                return;
            }
            Log.Warning($"CEDungeonInstanceSystem: no active entry for '{inst.PrototypeId}', using map origin.");
        }

        foreach (var player in group)
        {
            if (!Exists(player) || Deleted(player))
                continue;

            _transform.SetMapCoordinates(player, targetCoords);
        }

        inst.PlayerCount += group.Count;
        Log.Info($"CEDungeonInstanceSystem: transitioned {group.Count} player(s) to '{inst.PrototypeId}'.");
    }
}
