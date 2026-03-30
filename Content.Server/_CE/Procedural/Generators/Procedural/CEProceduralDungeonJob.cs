using System.Threading;
using System.Threading.Tasks;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Async job that generates a procedural dungeon across multiple frames,
/// yielding periodically via <see cref="Job{T}.SuspendIfOutOfTime"/> to
/// avoid blocking the main thread.
/// </summary>
public sealed class CEProceduralDungeonJob : Job<CEDungeonGenerateResult>
{
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _proto;
    private readonly IRobustRandom _random;
    private readonly SharedMapSystem _maps;
    private readonly CEZLevelsSystem _zLevels;
    private readonly CEProceduralGeneratorSystem _generator;
    private readonly CEProceduralConfig _config;
    private readonly ISawmill _sawmill;

    public CEProceduralDungeonJob(
        ISawmill sawmill,
        double maxTime,
        IEntityManager entManager,
        IPrototypeManager proto,
        IRobustRandom random,
        SharedMapSystem maps,
        CEZLevelsSystem zLevels,
        CEProceduralGeneratorSystem generator,
        CEProceduralConfig config,
        CancellationToken cancellation = default)
        : base(maxTime, cancellation)
    {
        _sawmill = sawmill;
        _entManager = entManager;
        _proto = proto;
        _random = random;
        _maps = maps;
        _zLevels = zLevels;
        _generator = generator;
        _config = config;
    }

    protected override async Task<CEDungeonGenerateResult> Process()
    {
        var config = _config;

        // Determine how many rooms to generate.
        var targetCount = _random.Next(config.GeneralCount.Min, config.GeneralCount.Max + 1);
        if (targetCount <= 0)
            return new CEDungeonGenerateResult(false);

        // Create a new map for this dungeon.
        var mapUid = _maps.CreateMap(out var mapId);

        // Build the abstract room graph.
        var comp = _entManager.AddComponent<CEGeneratingProceduralDungeonComponent>(mapUid);

        await _generator.BuildRoomGraph(comp, config.MaxRoomSize, targetCount, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        // Assign room types before selecting real prototypes.
        _generator.AssignRoomTypes(comp, config);
        await SuspendIfOutOfTime();

        // Add cyclic connections between adjacent General rooms (farthest from center first).
        var cycleCount = _random.Next(config.CycleCount.Min, config.CycleCount.Max + 1);
        _generator.AddCyclicConnections(comp, cycleCount, _random);
        await SuspendIfOutOfTime();

        // Assign real room prototypes, apply rotation, resize and randomize position.
        await _generator.AssignRealRooms(comp, config, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        // Compact: slide rooms toward their parent (BFS order), maintaining adaptive gap.
        await _generator.CompactRooms(comp, config.MainZLevel, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        // Create z-network so 3D rooms can be spawned across z-levels.
        var network = _zLevels.CreateZNetwork(config.Components);

        // Determine the maximum room height to know how many z-levels we need.
        var maxHeight = 1;
        foreach (var room in comp.Rooms)
        {
            if (room.RoomProtoId == null)
                continue;

            if (_proto.TryIndex(room.RoomProtoId.Value, out var rp) && rp.Height > maxHeight)
                maxHeight = rp.Height;
        }

        // Create a map for each required z-level and register them in the network.
        var mapsByDepth = new Dictionary<EntityUid, int>
        {
            { mapUid, 0 }
        };

        for (var zOffset = 1; zOffset < maxHeight; zOffset++)
        {
            var extraMapUid = _maps.CreateMap(out _);
            _entManager.EnsureComponent<MapGridComponent>(extraMapUid);
            mapsByDepth[extraMapUid] = zOffset;
        }

        _zLevels.TryAddMapsIntoZNetwork(network, mapsByDepth);
        await SuspendIfOutOfTime();

        // Ensure the map has a grid for tile/entity placement.
        var grid = _entManager.EnsureComponent<MapGridComponent>(mapUid);

        // Spawn each room's 3D prototype onto the grid.
        var reservedTiles = new HashSet<Vector2i>();
        await _generator.SpawnRooms(comp, mapUid, grid, reservedTiles, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        // Resolve the grid at MainZLevel for corridor placement.
        var corridorGridUid = mapUid;
        var corridorGrid = grid;

        if (config.MainZLevel != 0)
        {
            if (_zLevels.TryMapOffset(
                    (mapUid, _entManager.EnsureComponent<CEZLevelMapComponent>(mapUid)),
                    config.MainZLevel,
                    out var mainLevelMap))
            {
                corridorGridUid = mainLevelMap.Value;
                corridorGrid = _entManager.EnsureComponent<MapGridComponent>(corridorGridUid);
            }
            else
            {
                _sawmill.Warning(
                    $"CEProceduralDungeonJob: could not resolve MainZLevel {config.MainZLevel} for corridors.");
            }
        }

        var rng = new Random(_random.Next());

        // Build corridors and spawn doors between connected rooms.
        await _generator.BuildCorridors(comp, config, corridorGridUid, corridorGrid, rng, reservedTiles, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        // Place walls around the perimeter of all rooms and corridors on every z-level.
        await _generator.PlaceWalls(config, mapUid, mapsByDepth, reservedTiles, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        _entManager.Dirty(mapUid, comp);

        return new CEDungeonGenerateResult(true, mapUid, mapId);
    }
}
