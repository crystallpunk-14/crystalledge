using Content.Shared._CE.Maths;
using Content.Shared._CE.WorldGen.PlanSteps;
using Content.Shared._CE.WorldGen.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.PlanSteps;

/// <summary>
/// Paints a 3D box of chunks with one chunk type, from one chunk corner to another (inclusive
/// on all three axes). This is a full 3D planner step — boxes span X, Y and chunk-Z, so you can
/// paint volumes (a slab, a column, a cube). The world grows to whatever the plan paints.
/// </summary>
public sealed partial class CEPaintRect : CEWorldPlanStepBase<CEPaintRect>
{
    /// <summary>
    /// One corner of the box (x, y, chunkZ), inclusive.
    /// </summary>
    [DataField]
    public Vector3i From;

    /// <summary>
    /// Opposite corner of the box (x, y, chunkZ), inclusive.
    /// </summary>
    [DataField]
    public Vector3i To;

    /// <summary>
    /// Chunk type painted onto every cell of the box.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CEWorldChunkTypePrototype> ChunkType;
}

/// <summary>
/// Executes <see cref="CEPaintRect"/>: paints <see cref="CEPaintRect.ChunkType"/> across the
/// whole 3D box spanning From..To.
/// </summary>
public sealed class CEPaintRectSystem : CEWorldPlanStepSystem<CEPaintRect>
{
    protected override void Plan(ref CEWorldPlanStepEvent<CEPaintRect> args)
    {
        SetChunkType(args.Args, args.Step.From, args.Step.To, args.Step.ChunkType);
    }
}
