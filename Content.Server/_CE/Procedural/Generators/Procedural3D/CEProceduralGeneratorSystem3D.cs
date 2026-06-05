using System.Threading;
using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.Generators.Procedural3D.GenerationSteps;
using Content.Server._CE.ZLevels.Core;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural3D;

/// <summary>
/// Configuration for the 3D procedural dungeon generator.
/// Unlike <see cref="Procedural.CEProceduralConfig"/>, this generator builds dungeons across multiple
/// z-levels and connects rooms both horizontally and vertically.
/// </summary>
public sealed partial class CEProceduralConfig3D : CEDungeonGeneratorConfigBase<CEProceduralConfig3D>
{
    /// <summary>
    /// The ordered list of abstract generation steps.
    /// Executed sequentially to build the full 3D room graph before any rooms are placed.
    /// </summary>
    [DataField]
    public List<CEDungeonGenerationStep3D> GenerationPlan = new();
}

/// <summary>
/// Handles generation requests for <see cref="CEProceduralConfig3D"/> by creating
/// and dispatching a <see cref="CEProceduralDungeonJob3D"/>.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem3D : CEDungeonGeneratorSystem<CEProceduralConfig3D>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;

    protected override Job<CEDungeonGenerateResult> CreateJob(
        CEProceduralConfig3D config,
        double maxTime,
        CancellationToken cancellation)
    {
        return new CEProceduralDungeonJob3D(
            Log,
            maxTime,
            EntityManager,
            _zLevels,
            _random,
            config,
            cancellation);
    }
}
