using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: abstract room graph construction on a logical 2D grid
/// with frontier-based expansion.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem
{
    /// <summary>
    /// Checks whether a room at the given grid coordinate has at least one empty
    /// cardinal neighbour, i.e. it can still be expanded from.
    /// </summary>
    private static bool HasEmptyNeighbor(Vector2i gridCoord, HashSet<Vector2i> occupied)
    {
        foreach (var dir in Directions)
        {
            if (!occupied.Contains(gridCoord + dir))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Builds the abstract room graph on a logical 2D grid.
    /// Each room occupies exactly one grid cell. The world-tile position is
    /// <c>gridCoord * (maxRoomSize + 1)</c>, where the +1 accounts for
    /// a 1-tile gap between adjacent rooms.
    /// Uses a cached frontier (rooms with at least one free neighbour) so that
    /// every iteration is guaranteed to make progress — no wasted attempts.
    /// </summary>
    internal async Task BuildRoomGraph(
        CEGeneratingProceduralDungeonComponent comp,
        int maxRoomSize,
        int targetCount,
        Func<ValueTask> suspend)
    {
        var step = maxRoomSize + 1; // +1 for the gap tile
        var roomSize = new Vector2i(maxRoomSize, maxRoomSize);

        // Set of occupied logical grid cells for O(1) overlap checks.
        var occupied = new HashSet<Vector2i>();

        // Frontier: list of room indices whose grid cell still has >= 1 free cardinal neighbour.
        // We pick parents exclusively from this set, guaranteeing a valid expansion exists.
        var frontier = new List<int>();

        // Place the first room at grid (0, 0).
        var firstRoom = new CEProceduralAbstractRoom
        {
            Index = 0,
            GridCoord = Vector2i.Zero,
            Position = Vector2i.Zero,
            Size = roomSize,
        };
        comp.Rooms.Add(firstRoom);
        occupied.Add(Vector2i.Zero);
        frontier.Add(0);

        var yieldCounter = 0;

        while (comp.Rooms.Count < targetCount && frontier.Count > 0)
        {
            // Yield every 50 rooms to avoid blocking the main thread.
            if (++yieldCounter % 50 == 0)
                await suspend();

            // Pick a random frontier room to branch from.
            var frontierIdx = _random.Next(frontier.Count);
            var parentRoomIdx = frontier[frontierIdx];
            var parent = comp.Rooms[parentRoomIdx];

            // Collect free cardinal neighbours for this parent.
            var freeDirections = new List<Vector2i>();
            foreach (var dir in Directions)
            {
                if (!occupied.Contains(parent.GridCoord + dir))
                    freeDirections.Add(dir);
            }

            // Pick a random free direction.
            var chosenDir = _random.Pick(freeDirections);
            var newGridCoord = parent.GridCoord + chosenDir;

            // Place the new room.
            var newRoom = new CEProceduralAbstractRoom
            {
                Index = comp.Rooms.Count,
                GridCoord = newGridCoord,
                Position = new Vector2i(newGridCoord.X * step, newGridCoord.Y * step),
                Size = roomSize,
            };
            comp.Rooms.Add(newRoom);
            occupied.Add(newGridCoord);

            // Add a connection between parent and new room.
            comp.Connections.Add(new CEProceduralRoomConnection
            {
                RoomA = parentRoomIdx,
                RoomB = newRoom.Index,
            });

            // Add the new room to the frontier (it has at least 1 free neighbour –
            // the direction we came from is occupied, but the other 3 are likely free).
            if (HasEmptyNeighbor(newGridCoord, occupied))
                frontier.Add(newRoom.Index);

            // The parent may no longer belong to the frontier if all its
            // neighbours are now occupied.
            if (!HasEmptyNeighbor(parent.GridCoord, occupied))
            {
                // Swap-remove for O(1) removal from the frontier list.
                frontier[frontierIdx] = frontier[^1];
                frontier.RemoveAt(frontier.Count - 1);
            }

            // The newly placed room may also have blocked a previously-frontier
            // neighbour. Check the 4 neighbours of the new grid coord and evict
            // any that are no longer expandable.
            foreach (var dir in Directions)
            {
                var neighborCoord = newGridCoord + dir;
                if (neighborCoord == parent.GridCoord)
                    continue; // Already handled above.

                if (!occupied.Contains(neighborCoord))
                    continue; // Not a room.

                if (HasEmptyNeighbor(neighborCoord, occupied))
                    continue; // Still has room to expand.

                // Find this neighbour in the frontier and evict it.
                // Rooms are indexed by their Index field; find by grid coord.
                for (var fi = frontier.Count - 1; fi >= 0; fi--)
                {
                    if (comp.Rooms[frontier[fi]].GridCoord == neighborCoord)
                    {
                        frontier[fi] = frontier[^1];
                        frontier.RemoveAt(frontier.Count - 1);
                        break;
                    }
                }
            }
        }
    }
}
