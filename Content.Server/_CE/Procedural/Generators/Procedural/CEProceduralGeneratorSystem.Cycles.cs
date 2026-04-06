using Content.Shared._CE.Procedural;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: cyclic route injection.
/// Adds extra connections between adjacent General rooms that are not already connected,
/// prioritizing pairs that are farthest from the dungeon center (grid origin).
/// This creates loops in the otherwise tree-shaped room graph.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem
{
    /// <summary>
    /// Scans all pairs of grid-adjacent General rooms that are not yet connected,
    /// sorts them by combined Manhattan distance from center (descending),
    /// and adds up to <paramref name="cycleCount"/> extra connections.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>AssignRoomTypes</c> (so room types are known)
    /// and before <c>AssignRealRooms</c> (so room prototypes account for the extra exits).
    /// </remarks>
    internal void AddCyclicConnections(
        CEGeneratingProceduralDungeonComponent comp,
        int cycleCount,
        IRobustRandom random)
    {
        if (cycleCount <= 0)
            return;

        // Build a set of existing connections for O(1) lookup.
        var existingConnections = new HashSet<(int, int)>(comp.Connections.Count);
        foreach (var conn in comp.Connections)
        {
            // Store both orderings so we can check either direction.
            existingConnections.Add((conn.RoomA, conn.RoomB));
            existingConnections.Add((conn.RoomB, conn.RoomA));
        }

        // Build a grid-coord → room-index lookup for O(1) neighbour checks.
        var gridLookup = new Dictionary<Vector2i, int>(comp.Rooms.Count);
        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            gridLookup[comp.Rooms[i].GridCoord] = i;
        }

        // Find all candidate pairs: grid-adjacent General rooms that are not connected.
        // We store (roomA, roomB) with roomA < roomB to avoid duplicates.
        var candidates = new List<(int RoomA, int RoomB, int CombinedDistance)>();

        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            var room = comp.Rooms[i];
            if (room.RoomType != CEProceduralRoomType.General)
                continue;

            foreach (var dir in Directions)
            {
                var neighborCoord = room.GridCoord + dir;
                if (!gridLookup.TryGetValue(neighborCoord, out var neighborIdx))
                    continue;

                // Avoid duplicate pairs.
                if (neighborIdx <= i)
                    continue;

                var neighbor = comp.Rooms[neighborIdx];
                if (neighbor.RoomType != CEProceduralRoomType.General)
                    continue;

                // Skip if already connected.
                if (existingConnections.Contains((i, neighborIdx)))
                    continue;

                // Combined Manhattan distance from center.
                var distA = Math.Abs(room.GridCoord.X) + Math.Abs(room.GridCoord.Y);
                var distB = Math.Abs(neighbor.GridCoord.X) + Math.Abs(neighbor.GridCoord.Y);
                candidates.Add((i, neighborIdx, distA + distB));
            }
        }

        if (candidates.Count == 0)
            return;

        // Sort by combined distance descending (farthest pairs first).
        candidates.Sort((a, b) => b.CombinedDistance.CompareTo(a.CombinedDistance));

        // Take up to cycleCount connections.
        var count = Math.Min(cycleCount, candidates.Count);
        for (var i = 0; i < count; i++)
        {
            var (roomA, roomB, _) = candidates[i];
            comp.Connections.Add(new CEProceduralRoomConnection
            {
                RoomA = roomA,
                RoomB = roomB,
            });
        }

        Log.Debug($"AddCyclicConnections: added {count} cyclic connections ({candidates.Count} candidates).");
    }
}
