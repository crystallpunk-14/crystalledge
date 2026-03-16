using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Robust.Shared.Map;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: room compaction — slides rooms toward their parents in BFS order.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem
{
    /// <summary>
    /// Slides every room toward its parent room in BFS order from the root.
    /// The root room (index 0 / exit) stays in place. Each child is slid toward
    /// the centre of its parent as close as possible while keeping a 1-tile gap
    /// to every other room. This prevents connection lines from passing through
    /// unrelated rooms.
    /// </summary>
    internal static async Task CompactRooms(CEGeneratingProceduralDungeonComponent comp, Func<ValueTask> suspend)
    {
        if (comp.Rooms.Count == 0)
            return;

        const int gap = 1;

        // Build adjacency list and determine parent via BFS from root (index 0).
        var adj = new Dictionary<int, List<int>>();
        foreach (var conn in comp.Connections)
        {
            if (!adj.TryGetValue(conn.RoomA, out var listA))
            {
                listA = new List<int>();
                adj[conn.RoomA] = listA;
            }
            listA.Add(conn.RoomB);

            if (!adj.TryGetValue(conn.RoomB, out var listB))
            {
                listB = new List<int>();
                adj[conn.RoomB] = listB;
            }
            listB.Add(conn.RoomA);
        }

        // BFS to get processing order and parent map.
        var parent = new Dictionary<int, int>(); // child -> parent index
        var bfsOrder = new List<int>();
        var visited = new HashSet<int>();
        var queue = new Queue<int>();

        queue.Enqueue(0);
        visited.Add(0);
        bfsOrder.Add(0);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adj.TryGetValue(current, out var neighbors))
                continue;

            foreach (var neighbor in neighbors)
            {
                if (visited.Contains(neighbor))
                    continue;

                visited.Add(neighbor);
                parent[neighbor] = current;
                bfsOrder.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        // Index rooms by their index for quick lookup.
        var roomByIndex = new Dictionary<int, CEProceduralAbstractRoom>();
        foreach (var room in comp.Rooms)
            roomByIndex[room.Index] = room;

        // Process rooms in BFS order. Root stays in place; children slide toward parent.
        var compactCounter = 0;
        foreach (var roomIdx in bfsOrder)
        {
            // Yield every 20 rooms — each room runs a slide loop.
            if (++compactCounter % 20 == 0)
                await suspend();
            if (!roomByIndex.TryGetValue(roomIdx, out var room))
                continue;

            // Root room: no parent, leave as-is.
            if (!parent.TryGetValue(roomIdx, out var parentIdx))
                continue;

            if (!roomByIndex.TryGetValue(parentIdx, out var parentRoom))
                continue;

            // Compute the ideal adjacent target based on the grid direction
            // the child was originally branched in. This places the child right
            // next to the parent, centered on the perpendicular axis.
            var gridDir = room.GridCoord - parentRoom.GridCoord;

            int targetX;
            int targetY;

            if (gridDir.X > 0) // child is to the right of parent
            {
                targetX = parentRoom.Position.X + parentRoom.Size.X + gap;
                targetY = parentRoom.Position.Y + (parentRoom.Size.Y - room.Size.Y) / 2;
            }
            else if (gridDir.X < 0) // child is to the left
            {
                targetX = parentRoom.Position.X - room.Size.X - gap;
                targetY = parentRoom.Position.Y + (parentRoom.Size.Y - room.Size.Y) / 2;
            }
            else if (gridDir.Y > 0) // child is above parent
            {
                targetX = parentRoom.Position.X + (parentRoom.Size.X - room.Size.X) / 2;
                targetY = parentRoom.Position.Y + parentRoom.Size.Y + gap;
            }
            else // child is below parent (gridDir.Y < 0)
            {
                targetX = parentRoom.Position.X + (parentRoom.Size.X - room.Size.X) / 2;
                targetY = parentRoom.Position.Y - room.Size.Y - gap;
            }

            // Determine step directions toward the target.
            var stepX = room.Position.X > targetX ? -1 : (room.Position.X < targetX ? 1 : 0);
            var stepY = room.Position.Y > targetY ? -1 : (room.Position.Y < targetY ? 1 : 0);

            // Alternate: 1 tile on X, 1 tile on Y. Stop only when both axes are blocked.
            var blockedX = stepX == 0;
            var blockedY = stepY == 0;

            while (!blockedX || !blockedY)
            {
                // --- Try 1 tile on X ---
                if (!blockedX)
                {
                    var nextX = room.Position.X + stepX;

                    // Overshoot check.
                    var overshoot = (stepX < 0 && nextX < targetX) || (stepX > 0 && nextX > targetX);

                    if (overshoot || WouldOverlap(comp.Rooms, room.Index, nextX, room.Position.Y, room.Size, gap))
                    {
                        blockedX = true;
                    }
                    else
                    {
                        room.Position = new Vector2i(nextX, room.Position.Y);
                    }
                }

                // --- Try 1 tile on Y ---
                if (!blockedY)
                {
                    var nextY = room.Position.Y + stepY;

                    var overshoot = (stepY < 0 && nextY < targetY) || (stepY > 0 && nextY > targetY);

                    if (overshoot || WouldOverlap(comp.Rooms, room.Index, room.Position.X, nextY, room.Size, gap))
                    {
                        blockedY = true;
                    }
                    else
                    {
                        room.Position = new Vector2i(room.Position.X, nextY);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks whether placing a room at (<paramref name="x"/>, <paramref name="y"/>)
    /// with the given <paramref name="size"/> would overlap any existing room
    /// (identified by index != <paramref name="selfIndex"/>), including a gap border.
    /// </summary>
    private static bool WouldOverlap(
        List<CEProceduralAbstractRoom> rooms,
        int selfIndex,
        int x,
        int y,
        Vector2i size,
        int gap)
    {
        // Expanded AABB of the candidate (including gap on all sides).
        var minX = x - gap;
        var minY = y - gap;
        var maxX = x + size.X + gap;
        var maxY = y + size.Y + gap;

        foreach (var other in rooms)
        {
            if (other.Index == selfIndex)
                continue;

            // Other room AABB (no expansion needed – the candidate already has the gap).
            var oMinX = other.Position.X;
            var oMinY = other.Position.Y;
            var oMaxX = other.Position.X + other.Size.X;
            var oMaxY = other.Position.Y + other.Size.Y;

            // Standard AABB overlap test.
            if (minX < oMaxX && maxX > oMinX && minY < oMaxY && maxY > oMinY)
                return true;
        }

        return false;
    }
}
