using Content.Server._CE.WorldGen.Components;
using Content.Server._CE.WorldGen.PlanSteps;
using Content.Server._CE.WorldGen.Prototypes;
using Content.Server._CE.ZLevels.Core;
using Content.Server.GameTicking.Events;
using Content.Shared._CE.Maths;
using Content.Shared._CE.ZLevels.Core.Components;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._CE.WorldGen;

/// <summary>
/// Finite, 3D, lazily-generated procedural world.
/// At round start it runs a config's plan steps to paint a 3D chunk map, creates one map per
/// z-level wired into a single z-network, then (in the ChunkLoad partial) generates chunks
/// around players on demand and unloads them when nobody is near, preserving player edits.
/// All world-gen logic is server-only.
/// </summary>
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

    /// <summary>Reused per-tick scratch: chunks each world wants loaded.</summary>
    private readonly Dictionary<EntityUid, HashSet<Vector3i>> _activeChunks = new();

    private readonly ObjectPool<HashSet<Vector3i>> _chunkSetPool =
        new DefaultObjectPool<HashSet<Vector3i>>(new SetPolicy<Vector3i>(), 64);

    /// <summary>How many z-levels a single chunk spans.</summary>
    public const int ChunkHeightLevels = 2;

    /// <summary>Config generated automatically at round start (MVP — could become a CVar).</summary>
    private static readonly ProtoId<CEWorldConfigPrototype> DefaultWorldConfig = "CETestWorld";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if (!_proto.TryIndex(DefaultWorldConfig, out var config))
            return;

        GenerateWorld(config);
    }

    /// <summary>
    /// Plans the world, creates its z-level maps and wires them into a fresh z-network.
    /// Chunks themselves are generated lazily later, around players.
    /// </summary>
    public void GenerateWorld(CEWorldConfigPrototype config)
    {
        var network = _zLevels.CreateZNetwork(config.MapComponents);
        var world = AddComp<CEWorldComponent>(network.Owner);
        world.Config = config.ID;
        world.Seed = config.Seed == 0 ? _random.Next() : config.Seed;
        world.ChunkSize = config.ChunkSizeTiles;
        world.LoadRadiusChunks = config.LoadRadiusChunks;

        // Planning: run each step to paint the chunk map.
        var planArgs = new CEWorldPlanArgs(EntityManager, world.ChunkMap, _random);
        foreach (var step in config.PlanSteps)
        {
            step.Execute(planArgs);
        }

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

        world.MinChunkZ = minZ;
        world.MaxChunkZ = maxZ;

        // Create one map per z-level and wire them into the network.
        var depthByMap = new Dictionary<EntityUid, int>();
        var minDepth = minZ * ChunkHeightLevels;
        var maxDepth = maxZ * ChunkHeightLevels + ChunkHeightLevels - 1;

        for (var depth = minDepth; depth <= maxDepth; depth++)
        {
            var mapUid = _map.CreateMap(out _);
            EnsureComp<MapGridComponent>(mapUid);
            _meta.SetEntityName(mapUid, $"World {config.ID} [{depth}]");
            world.ZLevelMaps[depth] = mapUid;
            depthByMap[mapUid] = depth;
        }

        _zLevels.TryAddMapsIntoZNetwork(network, depthByMap);
        _meta.SetEntityName(network.Owner, $"World z-Network: {config.ID}");

        var zCount = maxDepth - minDepth + 1;
        Log.Info($"CEWorldGen: world {config.ID} planned: {world.ChunkMap.Count} chunks across {zCount} z-levels (seed {world.Seed}).");
    }

    /// <summary>Integer floor division (handles negative numerators, positive divisor).</summary>
    private static int FloorDiv(int a, int b)
    {
        var q = a / b;
        if (a % b != 0 && (a < 0) != (b < 0))
            q--;

        return q;
    }
}
