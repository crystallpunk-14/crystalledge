using Content.Shared._CE.WorldGen.PlanSteps;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.WorldGen.Prototypes;

/// <summary>
/// Top-level recipe for a procedurally generated CE world.
/// The world has no fixed size: it is bounded only by the chunks the plan paints.
/// </summary>
[Prototype("worldConfig")]
public sealed partial class CEWorldConfigPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Ordered list of planner steps that paint chunk types onto the chunk grid.
    /// Server-only: concrete step types are not registered on the client.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<CEWorldPlanStep> PlanSteps = new();

    /// <summary>
    /// Components added to every z-level map (gravity, atmosphere, lighting, ...).
    /// </summary>
    [DataField]
    public ComponentRegistry MapComponents = new();
}
