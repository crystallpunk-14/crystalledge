using System.Threading.Tasks;

namespace Content.Server._CE.Procedural.Generators.Procedural.GenerationSteps;

/// <summary>
/// Abstract base for a single step in a procedural dungeon generation plan.
/// Each subtype defines its own data fields and implements <see cref="Execute"/>.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEDungeonGenerationStep
{
    /// <summary>
    /// Runs this step, mutating <see cref="CEGenerationStepContext.Comp"/> in place.
    /// </summary>
    public abstract Task Execute(CEGenerationStepContext context);
}
