using Content.Server._CE.WorldGen.Prototypes;
using Content.Shared._CE.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.WorldGen.PlanSteps;

/// <summary>
/// Data-only base class for world plan steps.
/// Steps run in order at round start, each painting chunk types onto the chunk map.
/// Logic is handled by systems subscribing to <see cref="CEWorldPlanStepEvent{T}"/>,
/// mirroring the CE entity-effect pattern (data + handling system in one file).
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEWorldPlanStep
{
    /// <summary>
    /// Dispatches this step by raising a typed broadcast event.
    /// </summary>
    public abstract void Execute(CEWorldPlanArgs args);
}

/// <summary>
/// Generic base that provides automatic event dispatch for concrete step types.
/// </summary>
public abstract partial class CEWorldPlanStepBase<T> : CEWorldPlanStep where T : CEWorldPlanStepBase<T>
{
    public override void Execute(CEWorldPlanArgs args)
    {
        if (this is not T typed)
            return;

        var ev = new CEWorldPlanStepEvent<T>(typed, args);
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }
}

/// <summary>
/// State and services shared across a single world-planning run.
/// The world has no fixed bounds — it grows to whatever the steps paint.
/// </summary>
public sealed class CEWorldPlanArgs(
    IEntityManager entityManager,
    Dictionary<Vector3i, ProtoId<CEWorldChunkTypePrototype>> chunkMap,
    IRobustRandom random)
{
    public readonly IEntityManager EntityManager = entityManager;

    /// <summary>
    /// The chunk map being painted: (x, y, chunkZ) -> chunk type.
    /// </summary>
    public readonly Dictionary<Vector3i, ProtoId<CEWorldChunkTypePrototype>> ChunkMap = chunkMap;

    /// <summary>
    /// Deterministic RNG for this run.
    /// </summary>
    public readonly IRobustRandom Random = random;
}

/// <summary>
/// Broadcast event raised when a world plan step is dispatched.
/// </summary>
[ByRefEvent]
public record struct CEWorldPlanStepEvent<T>(T Step, CEWorldPlanArgs Args) where T : CEWorldPlanStepBase<T>;

/// <summary>
/// Abstract base system for handling world plan steps.
/// Concrete systems inherit this, implement <see cref="Plan"/>, and use the protected
/// chunk-map API below to paint, clear and query cells instead of touching the dictionary directly.
/// </summary>
public abstract partial class CEWorldPlanStepSystem<T> : EntitySystem where T : CEWorldPlanStepBase<T>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEWorldPlanStepEvent<T>>(OnStep);
    }

    private void OnStep(ref CEWorldPlanStepEvent<T> args)
    {
        Plan(ref args);
    }

    protected abstract void Plan(ref CEWorldPlanStepEvent<T> args);

    // ── Chunk-map API ─────────────────────────────────────────────────────

    /// <summary>
    /// Paints a single chunk cell with a type.
    /// </summary>
    protected void SetChunkType(CEWorldPlanArgs args, Vector3i cell, ProtoId<CEWorldChunkTypePrototype> type)
    {
        args.ChunkMap[cell] = type;
    }

    /// <summary>
    /// Paints every cell of the inclusive 3D box From..To with a type.
    /// </summary>
    protected void SetChunkType(CEWorldPlanArgs args, Vector3i from, Vector3i to, ProtoId<CEWorldChunkTypePrototype> type)
    {
        foreach (var cell in EnumerateBox(from, to))
        {
            args.ChunkMap[cell] = type;
        }
    }

    /// <summary>
    /// Removes a single chunk cell (returns it to void). Returns true if it existed.
    /// </summary>
    protected bool ClearChunk(CEWorldPlanArgs args, Vector3i cell)
    {
        return args.ChunkMap.Remove(cell);
    }

    /// <summary>
    /// Removes every cell of the inclusive 3D box From..To (returns them to void).
    /// </summary>
    protected void ClearChunks(CEWorldPlanArgs args, Vector3i from, Vector3i to)
    {
        foreach (var cell in EnumerateBox(from, to))
        {
            args.ChunkMap.Remove(cell);
        }
    }

    /// <summary>
    /// True if a cell has been painted.
    /// </summary>
    protected bool HasChunk(CEWorldPlanArgs args, Vector3i cell)
    {
        return args.ChunkMap.ContainsKey(cell);
    }

    /// <summary>
    /// Gets the type painted at a cell, if any.
    /// </summary>
    protected bool TryGetChunkType(CEWorldPlanArgs args, Vector3i cell, out ProtoId<CEWorldChunkTypePrototype> type)
    {
        return args.ChunkMap.TryGetValue(cell, out type);
    }

    /// <summary>
    /// Enumerates every cell of the inclusive 3D box spanning two corners, in any order.
    /// </summary>
    protected static IEnumerable<Vector3i> EnumerateBox(Vector3i from, Vector3i to)
    {
        var minX = Math.Min(from.X, to.X);
        var maxX = Math.Max(from.X, to.X);
        var minY = Math.Min(from.Y, to.Y);
        var maxY = Math.Max(from.Y, to.Y);
        var minZ = Math.Min(from.Z, to.Z);
        var maxZ = Math.Max(from.Z, to.Z);

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                for (var z = minZ; z <= maxZ; z++)
                {
                    yield return new Vector3i(x, y, z);
                }
            }
        }
    }
}
