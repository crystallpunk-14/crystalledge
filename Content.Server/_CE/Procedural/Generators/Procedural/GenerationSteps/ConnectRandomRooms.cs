using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Generators.Procedural.GenerationSteps;

/// <summary>
/// Adds extra cyclic connections between adjacent rooms that match the given types.
/// Creates loops in the otherwise tree-shaped room graph.
/// Pairs are sorted by combined Manhattan distance from the grid origin (farthest first).
/// </summary>
[DataDefinition]
public sealed partial class ConnectRandomRooms : CEDungeonGenerationStep
{
    /// <summary>
    /// Only rooms whose current type is in this list are considered.
    /// Empty list means all rooms are eligible.
    /// </summary>
    [DataField]
    public List<ProtoId<CERoomTypePrototype>> RoomTypes = new();

    /// <summary>
    /// Number of extra connections to add (picked randomly in range, inclusive).
    /// </summary>
    [DataField]
    public MinMax Count = new(0, 0);

    /// <inheritdoc/>
    public override Task Execute(CEGenerationStepContext context)
    {
        var comp = context.Comp;
        var count = context.Random.Next(Count.Min, Count.Max + 1);

        if (count <= 0)
            return Task.CompletedTask;

        // Build existing connection set for O(1) lookup.
        var existingConnections = new HashSet<(int, int)>(comp.Connections.Count * 2);
        foreach (var conn in comp.Connections)
        {
            existingConnections.Add((conn.RoomA, conn.RoomB));
            existingConnections.Add((conn.RoomB, conn.RoomA));
        }

        // Build grid-coord → room-index lookup.
        var gridLookup = new Dictionary<Vector2i, int>(comp.Rooms.Count);
        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            gridLookup[comp.Rooms[i].GridCoord] = i;
        }

        // Find candidate pairs: grid-adjacent rooms matching the type filter that are not yet connected.
        var candidates = new List<(int RoomA, int RoomB, int CombinedDistance)>();

        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            var room = comp.Rooms[i];

            if (RoomTypes.Count > 0 && (room.RoomType == null || !RoomTypes.Contains(room.RoomType.Value)))
                continue;

            foreach (var dir in CEGenerationStepContext.Directions)
            {
                var neighborCoord = room.GridCoord + dir;
                if (!gridLookup.TryGetValue(neighborCoord, out var neighborIdx))
                    continue;

                if (neighborIdx <= i)
                    continue;

                var neighbor = comp.Rooms[neighborIdx];

                if (RoomTypes.Count > 0 && (neighbor.RoomType == null || !RoomTypes.Contains(neighbor.RoomType.Value)))
                    continue;

                if (existingConnections.Contains((i, neighborIdx)))
                    continue;

                var distA = Math.Abs(room.GridCoord.X) + Math.Abs(room.GridCoord.Y);
                var distB = Math.Abs(neighbor.GridCoord.X) + Math.Abs(neighbor.GridCoord.Y);
                candidates.Add((i, neighborIdx, distA + distB));
            }
        }

        if (candidates.Count == 0)
            return Task.CompletedTask;

        // Sort by combined distance descending (farthest pairs first).
        candidates.Sort((a, b) => b.CombinedDistance.CompareTo(a.CombinedDistance));

        var added = Math.Min(count, candidates.Count);
        for (var i = 0; i < added; i++)
        {
            var (roomA, roomB, _) = candidates[i];
            comp.Connections.Add(new CEProceduralRoomConnection
            {
                RoomA = roomA,
                RoomB = roomB,
            });
        }

        context.Log.Debug($"ConnectRandomRoomsStep: added {added} cyclic connections ({candidates.Count} candidates).");
        return Task.CompletedTask;
    }
}
