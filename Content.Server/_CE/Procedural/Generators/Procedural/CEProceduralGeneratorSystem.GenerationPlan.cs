using System.Threading.Tasks;
using Content.Server._CE.Procedural.Generators.Procedural.GenerationSteps;
using Content.Shared._CE.Procedural;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: modular generation plan executor.
/// Iterates <see cref="CEProceduralConfig.GenerationPlan"/> and delegates each step to its own
/// <see cref="CEDungeonGenerationStep.Execute"/> implementation.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem
{
    /// <summary>
    /// Runs every step in <see cref="CEProceduralConfig.GenerationPlan"/> in order,
    /// yielding between steps.
    /// </summary>
    internal async Task ExecuteGenerationPlan(
        CEGeneratingProceduralDungeonComponent comp,
        CEProceduralConfig config,
        Func<ValueTask> suspend)
    {
        var ctx = new CEGenerationStepContext(comp, config.MaxRoomSize, _random, Log, suspend);

        foreach (var step in config.GenerationPlan)
        {
            await step.Execute(ctx);
            await suspend();
        }
    }
}
