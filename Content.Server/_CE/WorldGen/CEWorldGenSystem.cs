using System.Linq;
using Content.Shared._CE.WorldGen.Components;
using Content.Shared._CE.WorldGen.PlanSteps;
using Content.Shared._CE.WorldGen.Prototypes;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Maths;
using Content.Shared._CE.ZLevels.Core.Components;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.Player;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._CE.WorldGen;

public sealed partial class CEWorldGenSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!;

    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<CEWorldComponent> _worldQuery = default!;
    [Dependency] private EntityQuery<CEZLevelMapComponent> _zMapQuery = default!;

    /// <summary>
    /// Reused per-tick scratch: chunks each world wants loaded.
    /// </summary>
    private readonly Dictionary<EntityUid, HashSet<Vector3i>> _activeChunks = new();

    private readonly ObjectPool<HashSet<Vector3i>> _chunkSetPool =
        new DefaultObjectPool<HashSet<Vector3i>>(new SetPolicy<Vector3i>(), 64);

    public const int ChunkSize = CEWorldComponent.ChunkSize;
    public const int ChunkHeight = CEWorldComponent.ChunkHeight;
    public const float LoadRadiusChunks = CEWorldComponent.LoadRadiusChunks;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
    }

    public void CreateWorld(ProtoId<CEWorldConfigPrototype> configId)
    {
        if (!_proto.TryIndex(configId, out var config))
            return;

        var network = _zLevels.CreateZNetwork(config.MapComponents);
        var world = AddComp<CEWorldComponent>(network.Owner);
        world.Config = configId;
        world.Seed = _random.Next();

        // Planning: run each step to paint the chunk map.
        var planArgs = new CEWorldPlanArgs(EntityManager, world.ChunkMap, _random, world.Seed);
        foreach (var step in config.PlanSteps)
        {
            step.Execute(planArgs);
        }

        Dirty(network.Owner, world);

        if (world.ChunkMap.Count == 0)
        {
            Log.Warning($"CEWorldGen: config {config.ID} painted no chunks; nothing to generate.");
            return;
        }

        // Derive the vertical extent from the painted plan.
        var minZ = int.MaxValue;
        var maxZ = int.MinValue;
        foreach (var cell in world.ChunkMap.Keys)
        {
            minZ = Math.Min(minZ, cell.Z);
            maxZ = Math.Max(maxZ, cell.Z);
        }

        // Create one map per z-level and wire them into the network.
        var depthByMap = new Dictionary<EntityUid, int>();
        var minDepth = minZ * ChunkHeight;
        var maxDepth = maxZ * ChunkHeight + ChunkHeight - 1;

        for (var depth = minDepth; depth <= maxDepth; depth++)
        {
            var mapUid = _map.CreateMap(out _);
            EnsureComp<MapGridComponent>(mapUid);
            _meta.SetEntityName(mapUid, $"World {config.ID} [{depth}]");
            depthByMap[mapUid] = depth;
        }

        _zLevels.TryAddMapsIntoZNetwork(network, depthByMap);
        _meta.SetEntityName(network.Owner, $"World z-Network: {config.ID}");

        var zCount = maxDepth - minDepth + 1;
        Log.Info(
            $"CEWorldGen: world {config.ID} planned: {world.ChunkMap.Count} chunks across {zCount} z-levels (seed {world.Seed}).");

        // Preload pinned chunks immediately so they exist before player spawn.
        foreach (var pinned in world.PinnedChunks)
            LoadChunk((network.Owner, world), pinned);
    }

    /// <summary>
    /// Unloads all chunk content, re-paints the chunk map from current prototypes (same seed),
    /// and lets the update loop reload chunks fresh. Z-network and maps are preserved.
    /// </summary>
    public void RegenerateWorld()
    {
        var query = AllEntityQuery<CEWorldComponent>();
        while (query.MoveNext(out var uid, out var world))
        {
            // Clear player edits FIRST so UnloadChunk treats all tiles as pristine
            world.ModifiedTiles.Clear();

            // Unload all chunks — fully clean because ModifiedTiles is empty
            foreach (var cell in world.LoadedChunks.ToList())
            {
                UnloadChunk((uid, world), cell);
            }

            // Re-paint from current in-memory prototypes (keep same seed)
            world.ChunkMap.Clear();
            if (_proto.TryIndex(world.Config, out var config))
            {
                var planArgs = new CEWorldPlanArgs(EntityManager, world.ChunkMap, _random, world.Seed);
                foreach (var step in config.PlanSteps)
                {
                    step.Execute(planArgs);
                }
            }

            Dirty(uid, world);

            Log.Info($"CEWorldGen: world {world.Config} regenerated in-place (seed {world.Seed}).");
        }
    }

    /// <summary>
    /// On hot-reload of a <see cref="CEWorldChunkTypePrototype"/>, regenerates every currently-loaded
    /// chunk of that type so generator edits show up immediately (preserving player edits via the normal
    /// unload path). New chunks pick up the new data on their own. Mirrors biome ProtoReload.
    /// </summary>
    private void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        if (!args.TryGetModified<CEWorldChunkTypePrototype>(out var modifiedIds))
            return;

        List<Vector3i> reloadScratch = new();

        var query = AllEntityQuery<CEWorldComponent>();
        while (query.MoveNext(out var uid, out var world))
        {
            reloadScratch.Clear();

            foreach (var cell in world.LoadedChunks)
            {
                if (world.ChunkMap.TryGetValue(cell, out var type) && modifiedIds.Contains(type.Id))
                    reloadScratch.Add(cell);
            }

            foreach (var cell in reloadScratch)
            {
                UnloadChunk((uid, world), cell);
                LoadChunk((uid, world), cell);
            }
        }
    }

    /// <summary>
    /// Integer floor division (handles negative numerators, positive divisor).
    /// </summary>
    private static int FloorDiv(int a, int b)
    {
        var q = a / b;
        if (a % b != 0 && (a < 0) != (b < 0))
            q--;

        return q;
    }
}
