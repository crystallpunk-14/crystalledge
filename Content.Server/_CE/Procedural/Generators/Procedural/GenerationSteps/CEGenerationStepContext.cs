using Content.Shared._CE.Procedural;
using Robust.Shared.Log;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural.GenerationSteps;

/// <summary>
/// All services and state needed by a <see cref="CEDungeonGenerationStep"/> during execution.
/// </summary>
public sealed class CEGenerationStepContext
{
    /// <summary>Room graph being constructed in-place.</summary>
    public readonly CEGeneratingProceduralDungeonComponent Comp;

    /// <summary>Maximum side length of any single room in world tiles.</summary>
    public readonly int MaxRoomSize;

    /// <summary>Random number generator for this generation run.</summary>
    public readonly IRobustRandom Random;

    /// <summary>Logger for warnings / debug output.</summary>
    public readonly ISawmill Log;

    /// <summary>Cooperative async yield — call to hand control back to the scheduler.</summary>
    public readonly Func<ValueTask> Suspend;

    /// <summary>
    /// Cardinal direction vectors used for grid-neighbour queries.
    /// </summary>
    public static readonly Robust.Shared.Map.Vector2i[] Directions =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    public CEGenerationStepContext(
        CEGeneratingProceduralDungeonComponent comp,
        int maxRoomSize,
        IRobustRandom random,
        ISawmill log,
        Func<ValueTask> suspend)
    {
        Comp = comp;
        MaxRoomSize = maxRoomSize;
        Random = random;
        Log = log;
        Suspend = suspend;
    }

    /// <summary>
    /// Returns <c>true</c> if at least one of the four cardinal neighbours of
    /// <paramref name="gridCoord"/> is not in <paramref name="occupied"/>.
    /// </summary>
    public static bool HasEmptyNeighbor(Robust.Shared.Map.Vector2i gridCoord, HashSet<Robust.Shared.Map.Vector2i> occupied)
    {
        foreach (var dir in Directions)
            if (!occupied.Contains(gridCoord + dir))
                return true;
        return false;
    }
}
