using System.Threading;
using System.Threading.Tasks;
using Content.Server._CE.ZLevels.Core;
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
        if (config.GenerationPlan.Count == 0)
        {
            sawmill.Error("CEProceduralDungeonJob3D: GenerationPlan is empty, cannot generate dungeon.");
            return new CEDungeonGenerateResult(false);
        }

        var network = zLevels.CreateZNetwork();
        var comp = entManager.AddComponent<CEGeneratingProceduralDungeon3DComponent>(network.Owner);

        foreach (var step in config.GenerationPlan)
        {
            await step.Execute(comp, config.MaxRoomSize, config.MaxRoomHeight, random, sawmill);
        }

        if (comp.Rooms.Count == 0)
        {
            sawmill.Error("CEProceduralDungeonJob3D: GenerationPlan produced no rooms.");
            return new CEDungeonGenerateResult(false);
        }

        // Determine how many z-levels the room graph spans.
        var maxZ = 0;
        foreach (var room in comp.Rooms)
        {
            if (room.GridCoord.Z > maxZ)
                maxZ = room.GridCoord.Z;
        }

        // TODO: This z-level creation happens here for testability.
        // In the future a room compaction pass will shift rooms closer together,
        // potentially reducing the number of required levels. Z-level map creation
        // should be deferred until after compaction.
        // Each abstract Z grid cell spans MaxRoomHeight actual z-levels.
        // GridCoord.Z=0 → levels 0..Height-1, GridCoord.Z=1 → levels Height..2*Height-1, etc.
        var levelCount = (maxZ + 1) * config.MaxRoomHeight;
        var mapsByDepth = new Dictionary<EntityUid, int>(levelCount);

        // TODO: CEDungeonInstanceSystem currently requires a MapUid to register an instance.
        // Until it is refactored to use ZNetworkUid as the primary anchor, we expose the z=0
        // map as the primary map so the round-start and passage flows keep working.
        EntityUid primaryMapUid = default;
        MapId primaryMapId = default;

        for (var z = 0; z < levelCount; z++)
        {
            var mapUid = maps.CreateMap(out var mapId);
            entManager.EnsureComponent<MapGridComponent>(mapUid);
            mapsByDepth[mapUid] = z;

            if (z == 0)
            {
                primaryMapUid = mapUid;
                primaryMapId = mapId;
            }
        }

        zLevels.TryAddMapsIntoZNetwork(network, mapsByDepth);
        await SuspendIfOutOfTime();

        entManager.Dirty(network.Owner, comp);
        return new CEDungeonGenerateResult(true, primaryMapUid, primaryMapId, ZNetworkUid: network.Owner);
    }
}
