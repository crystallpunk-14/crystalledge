using System.Threading;
using System.Threading.Tasks;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Maths;
using Content.Shared._CE.Procedural;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural3D;

/// <summary>
/// Async job that builds a 3D procedural dungeon by executing the abstract generation plan,
/// then creating one map per required z-level and registering them in the z-network.
/// The <see cref="CEGeneratingProceduralDungeon3DComponent"/> is attached to the z-network
/// entity since the room graph spans multiple levels.
/// </summary>
public sealed class CEProceduralDungeonJob3D(
    ISawmill sawmill,
    double maxTime,
    IEntityManager entManager,
    SharedMapSystem maps,
    CEZLevelsSystem zLevels,
    IRobustRandom random,
    CEProceduralConfig3D config,
    CancellationToken cancellation = default)
    : Job<CEDungeonGenerateResult>(maxTime, cancellation)
{
    protected override async Task<CEDungeonGenerateResult> Process()
    {
        if (config.Planning.Count == 0)
        {
            sawmill.Error("CEProceduralDungeonJob3D: GenerationPlan is empty, cannot generate dungeon.");
            return new CEDungeonGenerateResult(false);
        }

        var network = zLevels.CreateZNetwork();
        var comp = entManager.AddComponent<CEGeneratingProceduralDungeon3DComponent>(network.Owner);

        // Grid-phase lookup: maps GridCoord → Rooms list index.
        // Built incrementally by steps that add rooms; discarded after the plan completes.
        var roomsByCoord = new Dictionary<Vector3i, int>();

        foreach (var step in config.Planning)
        {
            await step.Execute(comp, roomsByCoord, config.MaxRoomSize, config.MaxRoomHeight, random, sawmill);
        }

        if (comp.Rooms.Count == 0)
        {
            sawmill.Error("CEProceduralDungeonJob3D: GenerationPlan produced no rooms.");
            return new CEDungeonGenerateResult(false);
        }

        // Determine the full Z span of the room graph, including negative Z values.
        // Each abstract grid cell spans MaxRoomHeight actual z-levels.
        // depth = GridCoord.Z * MaxRoomHeight + localZ — may be negative, which is fine.
        var minZ = 0;
        var maxZ = 0;
        foreach (var room in comp.Rooms)
        {
            if (room.GridCoord.Z < minZ) minZ = room.GridCoord.Z;
            if (room.GridCoord.Z > maxZ) maxZ = room.GridCoord.Z;
        }

        var mapsByDepth = new Dictionary<EntityUid, int>((maxZ - minZ + 1) * config.MaxRoomHeight);

        // TODO: CEDungeonInstanceSystem currently requires a MapUid to register an instance.
        // Until it is refactored to use ZNetworkUid as the primary anchor, we expose the z=0
        // map as the primary map so the round-start and passage flows keep working.
        EntityUid primaryMapUid = default;
        MapId primaryMapId = default;

        for (var gridZ = minZ; gridZ <= maxZ; gridZ++)
        {
            for (var localZ = 0; localZ < config.MaxRoomHeight; localZ++)
            {
                var depth = gridZ * config.MaxRoomHeight + localZ;
                var mapUid = maps.CreateMap(out var mapId);
                entManager.EnsureComponent<MapGridComponent>(mapUid);
                mapsByDepth[mapUid] = depth;

                if (depth == 0)
                {
                    primaryMapUid = mapUid;
                    primaryMapId = mapId;
                }
            }
        }

        zLevels.TryAddMapsIntoZNetwork(network, mapsByDepth);
        await SuspendIfOutOfTime();

        entManager.Dirty(network.Owner, comp);
        return new CEDungeonGenerateResult(true, primaryMapUid, primaryMapId, ZNetworkUid: network.Owner);
    }
}
