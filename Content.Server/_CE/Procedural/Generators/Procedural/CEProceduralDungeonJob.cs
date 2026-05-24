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
public sealed class CEProceduralDungeonJob(
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
    : Job<CEDungeonGenerateResult>(maxTime, cancellation)
{
    protected override async Task<CEDungeonGenerateResult> Process()
    {
        if (config.GenerationPlan.Count == 0)
        {
            sawmill.Error("CEProceduralDungeonJob: GenerationPlan is empty, cannot generate dungeon.");
            return new CEDungeonGenerateResult(false);
        }

        // Create a new map for this dungeon.
        var mapUid = maps.CreateMap(out var mapId);

        // Build the abstract room graph by executing every step in the generation plan.
        var comp = entManager.AddComponent<CEGeneratingProceduralDungeonComponent>(mapUid);

        await generator.ExecuteGenerationPlan(comp, config, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        if (comp.Rooms.Count == 0)
        {
            sawmill.Error("CEProceduralDungeonJob: GenerationPlan produced no rooms.");
            return new CEDungeonGenerateResult(false);
        }

        // Assign real room prototypes, apply rotation, resize and randomise position.
        await generator.AssignRealRooms(comp, config, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        // Compact: slide rooms toward their parent (BFS order), maintaining adaptive gap.
        await generator.CompactRooms(comp, config.MainZLevel, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        // Create z-network so 3D rooms can be spawned across z-levels.
        var network = zLevels.CreateZNetwork(config.Components);

        // Determine the maximum room height to know how many z-levels we need.
        var maxHeight = 1;
        foreach (var room in comp.Rooms)
        {
            if (room.RoomProtoId == null)
                continue;

            if (proto.TryIndex(room.RoomProtoId.Value, out var rp) && rp.Height > maxHeight)
                maxHeight = rp.Height;
        }

        // Create a map for each required z-level and register them in the network.
        var mapsByDepth = new Dictionary<EntityUid, int>
        {
            { mapUid, 0 }
        };

        for (var zOffset = 1; zOffset < maxHeight; zOffset++)
        {
            var extraMapUid = maps.CreateMap(out _);
            entManager.EnsureComponent<MapGridComponent>(extraMapUid);
            mapsByDepth[extraMapUid] = zOffset;
        }

        zLevels.TryAddMapsIntoZNetwork(network, mapsByDepth);
        await SuspendIfOutOfTime();

        // Ensure the map has a grid for tile/entity placement.
        var grid = entManager.EnsureComponent<MapGridComponent>(mapUid);

        // Spawn each room's 3D prototype onto the grid.
        var reservedTiles = new HashSet<Vector2i>();
        await generator.SpawnRooms(comp, mapUid, grid, reservedTiles, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        // Resolve the grid at MainZLevel for corridor placement.
        var corridorGridUid = mapUid;
        var corridorGrid = grid;

        if (config.MainZLevel != 0)
        {
            if (zLevels.TryMapOffset(
                    (mapUid, entManager.EnsureComponent<CEZLevelMapComponent>(mapUid)),
                    config.MainZLevel,
                    out var mainLevelMap))
            {
                corridorGridUid = mainLevelMap;
                corridorGrid = entManager.EnsureComponent<MapGridComponent>(corridorGridUid);
            }
            else
            {
                sawmill.Warning(
                    $"CEProceduralDungeonJob: could not resolve MainZLevel {config.MainZLevel} for corridors.");
            }
        }

        var rng = new Random(random.Next());

        // Build corridors and spawn doors between connected rooms.
        await generator.BuildCorridors(comp, config, corridorGridUid, corridorGrid, rng, reservedTiles, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        // Place walls around the perimeter of all rooms and corridors on every z-level.
        await generator.PlaceWalls(config, mapUid, mapsByDepth, reservedTiles, SuspendIfOutOfTime);
        await SuspendIfOutOfTime();

        entManager.Dirty(mapUid, comp);

        return new CEDungeonGenerateResult(true, mapUid, mapId, network.Owner);
    }
}
