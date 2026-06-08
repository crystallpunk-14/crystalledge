using System.Threading.Tasks;
using Content.Shared._CE.Maths;
using Content.Shared._CE.Procedural;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural3D.GenerationSteps;

/// <summary>
/// Appends rooms of type <see cref="Proto"/> to existing rooms of type <see cref="ConnectTo"/>,
/// placing each one in a free adjacent grid cell chosen uniformly at random.
///
/// Newly placed rooms whose type equals <see cref="ConnectTo"/> join the candidate pool
/// immediately, so this step can chain off its own output within a single execution.
/// </summary>
[DataDefinition]
public sealed partial class AppendRandom : CEDungeonGenerationStep3D
{
    /// <summary>Room type to place.</summary>
    [DataField(required: true)]
    public ProtoId<CERoomTypePrototype> Proto;

    /// <summary>Type of existing room to attach to.</summary>
    [DataField(required: true)]
    public ProtoId<CERoomTypePrototype> ConnectTo;

    /// <summary>Number of rooms to append (inclusive range, resolved once per execution).</summary>
    [DataField]
    public MinMax Count = new(1, 1);

    /// <summary>Allow attaching on the XY plane (±X, ±Y neighbours).</summary>
    [DataField]
    public bool ConnectSide = true;

    /// <summary>Allow attaching one level above (Z+1).</summary>
    [DataField]
    public bool ConnectRoof = false;

    /// <summary>Allow attaching one level below (Z-1).</summary>
    [DataField]
    public bool ConnectFloor = false;

    public override Task Execute(
        CEGeneratingProceduralDungeon3DComponent comp,
        Dictionary<Vector3i, int> roomsByCoord,
        int maxRoomSize,
        int maxRoomHeight,
        IRobustRandom random,
        ISawmill log)
    {
        var dirs = BuildDirections();
        if (dirs.Count == 0)
        {
            log.Warning("AppendRandom: no connection directions enabled.");
            return Task.CompletedTask;
        }

        // Seed candidates from rooms that already exist.
        var candidates = new List<Vector3i>();
        foreach (var room in comp.Rooms)
        {
            if (room.RoomType == ConnectTo)
                candidates.Add(room.GridCoord);
        }

        if (candidates.Count == 0)
        {
            log.Warning($"AppendRandom: no rooms of type '{ConnectTo}' found.");
            return Task.CompletedTask;
        }

        var target = Count.Next(random);
        for (var i = 0; i < target; i++)
        {
            // Collect every free neighbour across all candidates.
            // Gathering all options first gives a uniform distribution over free cells.
            var options = new List<(Vector3i from, Vector3i to)>();
            foreach (var cand in candidates)
            {
                foreach (var dir in dirs)
                {
                    var neighbor = cand + dir;
                    if (!roomsByCoord.ContainsKey(neighbor))
                        options.Add((cand, neighbor));
                }
            }

            if (options.Count == 0)
            {
                log.Warning($"AppendRandom: no free adjacent cell after {i} placements, stopping early.");
                break;
            }

            var (from, to) = options[random.Next(options.Count)];
            SetRoom(comp, roomsByCoord, to, maxRoomSize, maxRoomHeight, Proto);
            TryConnect(comp, roomsByCoord, from, to, log);

            // Immediately eligible as an attachment point for the next iteration.
            if (Proto == ConnectTo)
                candidates.Add(to);
        }

        return Task.CompletedTask;
    }

    private List<Vector3i> BuildDirections()
    {
        var dirs = new List<Vector3i>(6);
        if (ConnectSide)
        {
            dirs.Add(new Vector3i(1, 0, 0));
            dirs.Add(new Vector3i(-1, 0, 0));
            dirs.Add(new Vector3i(0, 1, 0));
            dirs.Add(new Vector3i(0, -1, 0));
        }
        if (ConnectRoof) dirs.Add(new Vector3i(0, 0, 1));
        if (ConnectFloor) dirs.Add(new Vector3i(0, 0, -1));
        return dirs;
    }
}
