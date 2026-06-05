using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural3D.GenerationSteps;

/// <summary>
/// Abstract base for a single step in a 3D procedural dungeon generation plan.
/// Steps operate purely on abstract graph data — no entity spawning — so no
/// cooperative yielding is needed.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEDungeonGenerationStep3D
{
    public abstract Task Execute(
        CEGeneratingProceduralDungeonComponent3D comp,
        IRobustRandom random,
        ISawmill log);
}
