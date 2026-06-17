using Content.Shared._CE.Maths;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.WorldGen.Components;
using Content.Shared._CE.WorldGen.PlanSteps;
using Content.Shared._CE.WorldGen.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.PlanSteps;

/// <summary>
/// Stamps a single pre-mapped room onto the chunk map, covering however many chunks its size requires.
/// All chunks in the region receive the <c>CEWorldStaticRegion</c> chunk type, and per-chunk data is
/// written to <see cref="CEWorldComponent.StaticRegions"/> for the generator to read at load time.
/// Room size must be divisible by <see cref="CEWorldComponent.ChunkSize"/> on both axes; if not, the
/// step is skipped with an error.
/// </summary>
public sealed partial class CEStampStructure : CEWorldPlanStepBase<CEStampStructure>
{
    /// <summary>
    /// Room to stamp. Its size determines the chunk footprint.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CEDungeonRoom3DPrototype> Room;

    /// <summary>
    /// Bottom-left (min XY) chunk coordinate of the stamped region.
    /// </summary>
    [DataField(required: true)]
    public Vector3i At;

    /// <summary>
    /// Fixed rotation in 90° steps (0–3). Ignored when <see cref="RandomRotation"/> is true.
    /// </summary>
    [DataField]
    public int RotationSteps;

    /// <summary>
    /// When true, picks a random rotation at stamp time (overrides <see cref="RotationSteps"/>).
    /// </summary>
    [DataField]
    public bool RandomRotation;
}

/// <summary>
/// Executes <see cref="CEStampStructure"/>: validates the room, resolves rotation, paints
/// <c>CEWorldStaticRegion</c> onto every chunk in the footprint, and writes per-chunk entries to
/// <see cref="CEWorldComponent.StaticRegions"/> so the generator can locate the correct room slice.
/// </summary>
public sealed class CEStampStructureSystem : CEWorldPlanStepSystem<CEStampStructure>
{
    [Dependency] private IPrototypeManager _protoMan = default!;

    protected override void Plan(ref CEWorldPlanStepEvent<CEStampStructure> args)
    {
        var step = args.Step;
        var planArgs = args.Args;

        var rot = step.RandomRotation
            ? new Random(HashCode.Combine(planArgs.Seed, step.At.X, step.At.Y, step.At.Z)).Next(4)
            : Math.Clamp(step.RotationSteps, 0, 3);

        var room = _protoMan.Index(step.Room);
        var cs = CEWorldComponent.ChunkSize;

        if (room.Size.X % cs != 0 || room.Size.Y % cs != 0)
        {
            Log.Error(
                $"CEStampStructure: room '{step.Room}' size {room.Size} is not chunk-aligned " +
                $"(must be divisible by {cs} on both axes). Skipping.");
            return;
        }

        // Rotation 90°/270° swaps the XY footprint.
        int regionX, regionY;
        if (rot % 2 == 0)
        {
            regionX = room.Size.X / cs;
            regionY = room.Size.Y / cs;
        }
        else
        {
            regionX = room.Size.Y / cs;
            regionY = room.Size.X / cs;
        }

        // Each z-level of the room maps to one chunk-Z (ChunkHeight == 1).
        var regionZ = room.Height;

        var worldQuery = EntityManager.EntityQueryEnumerator<CEWorldComponent>();
        if (!worldQuery.MoveNext(out _, out var world))
        {
            Log.Error("CEStampStructure: no CEWorldComponent found. Cannot write static regions.");
            return;
        }

        var entry = new CEStaticRegionEntry(step.Room, step.At, rot);

        for (var dx = 0; dx < regionX; dx++)
        {
            for (var dy = 0; dy < regionY; dy++)
            {
                for (var dz = 0; dz < regionZ; dz++)
                {
                    var coord = new Vector3i(step.At.X + dx, step.At.Y + dy, step.At.Z + dz);
                    SetChunkType(planArgs, coord, "CEWorldStaticRegion");
                    world.StaticRegions[coord] = entry;
                }
            }
        }
    }
}
