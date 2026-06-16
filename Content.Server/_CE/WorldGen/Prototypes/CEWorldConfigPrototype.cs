using Content.Server._CE.WorldGen.PlanSteps;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.Prototypes;

/// <summary>
/// Top-level recipe for a procedurally generated CE world.
/// The world has no fixed size: it is bounded only by the chunks the plan paints,
/// which configs may grow arbitrarily large. Chunks the plan never paints stay void,
/// and the vertical extent (number of z-levels) is derived from the painted plan.
/// Server-only, like the dungeon level prototypes — all world-gen logic lives on the server.
/// </summary>
[Prototype("ceWorldConfig")]
public sealed partial class CEWorldConfigPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Side length of one chunk in tiles.
    /// </summary>
    [DataField]
    public int ChunkSizeTiles = 8;

    /// <summary>
    /// World seed. 0 means a random seed is rolled at round start.
    /// </summary>
    [DataField]
    public int Seed;

    /// <summary>
    /// Ordered list of planner steps that paint chunk types onto the chunk grid.
    /// </summary>
    [DataField]
    public List<CEWorldPlanStep> PlanSteps = new();

    /// <summary>
    /// Components added to every z-level map (gravity, atmosphere, lighting, ...).
    /// </summary>
    [DataField]
    public ComponentRegistry MapComponents = new();

    /// <summary>
    /// How many chunks out from each player to keep loaded.
    /// </summary>
    [DataField]
    public float LoadRadiusChunks = 2f;
}
