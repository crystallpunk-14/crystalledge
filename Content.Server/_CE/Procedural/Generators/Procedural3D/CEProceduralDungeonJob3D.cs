using System.Threading;
using System.Threading.Tasks;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Procedural;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural3D;

/// <summary>
/// Async job that builds a 3D procedural dungeon by executing the abstract generation plan.
/// At this stage only the room graph is constructed — room spawning, corridor placement,
/// and z-level map creation are handled in later passes.
/// The <see cref="CEGeneratingProceduralDungeonComponent3D"/> is attached to the z-network
/// entity rather than a map, as the graph spans multiple levels.
/// </summary>
public sealed class CEProceduralDungeonJob3D(
    ISawmill sawmill,
    double maxTime,
    IEntityManager entManager,
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

        // Create the z-network that will own this dungeon's multi-level structure.
        // Maps for individual z-levels will be added in a later pass.
        var network = zLevels.CreateZNetwork();

        var comp = entManager.AddComponent<CEGeneratingProceduralDungeonComponent3D>(network.Owner);

        foreach (var step in config.GenerationPlan)
        {
            await step.Execute(comp, random, sawmill);
        }

        if (comp.Rooms.Count == 0)
        {
            sawmill.Error("CEProceduralDungeonJob3D: GenerationPlan produced no rooms.");
            return new CEDungeonGenerateResult(false);
        }

        entManager.Dirty(network.Owner, comp);
        return new CEDungeonGenerateResult(true, ZNetworkUid: network.Owner);
    }
}
