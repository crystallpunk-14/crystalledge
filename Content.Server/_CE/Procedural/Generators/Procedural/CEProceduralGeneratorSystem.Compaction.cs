using System.Numerics;
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
    /// the centre of its parent as close as possible while keeping a gap
    /// to every other room.
    /// <para>
    /// Gap is determined per parent-child pair:
    /// <list type="bullet">
    ///   <item>1 tile — if passway markers face each other AND the child can
    ///         actually reach that gap-1 position without being blocked.</item>
    ///   <item>3 tiles — otherwise (including when gap-1 was attempted but
    ///         blocked by a third room), to guarantee space for a corridor.</item>
    /// </list>
    /// </para>
    /// </summary>
    internal async Task CompactRooms(
        CEGeneratingProceduralDungeonComponent comp,
        int mainZLevel,
        Func<ValueTask> suspend)
    {
        if (comp.Rooms.Count == 0)
            return;

        const int closeGap = 1;
        const int farGap = 3;

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

            var gridDir = room.GridCoord - parentRoom.GridCoord;
            var savedPosition = room.Position;

            // Check if passways would align at the close-gap position.
            var closeTarget = ComputeTargetPosition(room, parentRoom, gridDir, closeGap);
            var passWouldAlign = HasAlignedPassways(room, closeTarget, parentRoom, mainZLevel);

            if (passWouldAlign)
            {
                // Try sliding to gap=1 target.
                SlideRoom(room, closeTarget, comp.Rooms, room.Index, parentIdx, closeGap, farGap);

                // Verify passways actually align at the final position.
                if (HasAlignedPassways(room, room.Position, parentRoom, mainZLevel))
                    continue; // Close connection achieved — gap=1.

                // Failed to reach alignment: reset and fall back to gap=3.
                room.Position = savedPosition;
            }

            // Slide with gap=3 (corridor mode).
            var farTarget = ComputeTargetPosition(room, parentRoom, gridDir, farGap);
            SlideRoom(room, farTarget, comp.Rooms, room.Index, parentIdx, farGap, farGap);
        }

        // After all rooms are placed, mark each connection as close/far
        // by checking passway alignment at final positions.
        foreach (var conn in comp.Connections)
        {
            if (!roomByIndex.TryGetValue(conn.RoomA, out var rA) ||
                !roomByIndex.TryGetValue(conn.RoomB, out var rB))
                continue;

            conn.IsClose = HasAlignedPassways(rA, rA.Position, rB, mainZLevel);
        }
    }

    /// <summary>
    /// Slides a room one tile at a time toward <paramref name="target"/>,
    /// alternating X and Y moves, stopping when blocked or at target.
    /// </summary>
    private static void SlideRoom(
        CEProceduralAbstractRoom room,
        Vector2i target,
        List<CEProceduralAbstractRoom> rooms,
        int selfIndex,
        int parentIndex,
        int parentGap,
        int defaultGap)
    {
        var stepX = room.Position.X > target.X ? -1 : (room.Position.X < target.X ? 1 : 0);
        var stepY = room.Position.Y > target.Y ? -1 : (room.Position.Y < target.Y ? 1 : 0);

        var blockedX = stepX == 0;
        var blockedY = stepY == 0;

        while (!blockedX || !blockedY)
        {
            if (!blockedX)
            {
                var nextX = room.Position.X + stepX;
                var overshoot = (stepX < 0 && nextX < target.X) || (stepX > 0 && nextX > target.X);

                if (overshoot || WouldOverlap(rooms, selfIndex, parentIndex, nextX, room.Position.Y, room.Size, parentGap, defaultGap))
                    blockedX = true;
                else
                    room.Position = new Vector2i(nextX, room.Position.Y);
            }

            if (!blockedY)
            {
                var nextY = room.Position.Y + stepY;
                var overshoot = (stepY < 0 && nextY < target.Y) || (stepY > 0 && nextY > target.Y);

                if (overshoot || WouldOverlap(rooms, selfIndex, parentIndex, room.Position.X, nextY, room.Size, parentGap, defaultGap))
                    blockedY = true;
                else
                    room.Position = new Vector2i(room.Position.X, nextY);
            }
        }
    }

    /// <summary>
    /// Computes the ideal target position for a child room relative to its parent,
    /// placing it adjacent on the connecting axis with the given <paramref name="gap"/>
    /// and centred on the perpendicular axis.
    /// </summary>
    private static Vector2i ComputeTargetPosition(
        CEProceduralAbstractRoom room,
        CEProceduralAbstractRoom parentRoom,
        Vector2i gridDir,
        int gap)
    {
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

        return new Vector2i(targetX, targetY);
    }

    /// <summary>
    /// Checks whether any passway marker from the child room (placed at
    /// <paramref name="childPosition"/>) faces a passway marker of the parent room
    /// across a 1-tile gap. Two passways are "aligned" when they face opposite
    /// directions and the tile just outside each one is the same (the shared gap tile).
    /// </summary>
    private bool HasAlignedPassways(
        CEProceduralAbstractRoom child,
        Vector2i childPosition,
        CEProceduralAbstractRoom parent,
        int mainZLevel)
    {
        var childPws = GetPasswayWorldTiles(child, childPosition, mainZLevel);
        var parentPws = GetPasswayWorldTiles(parent, parent.Position, mainZLevel);

        foreach (var (posP, dirP) in parentPws)
        {
            var outsideP = posP + dirP.ToIntVec();

            foreach (var (posC, dirC) in childPws)
            {
                // Passways must face opposite directions.
                if (!IsOppositeCardinal(dirP, dirC))
                    continue;

                // The "outside" tiles must coincide — this means the passway tiles
                // are separated by exactly 1 tile (the shared gap tile).
                if (outsideP == posC + dirC.ToIntVec())
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Computes world tile positions and rotated facing directions of all passway
    /// markers in a room placed at an explicit <paramref name="position"/>.
    /// Assumes TileSize = 1 (standard for SS14).
    /// </summary>
    private List<(Vector2i WorldTilePos, Direction FacingDir)> GetPasswayWorldTiles(
        CEProceduralAbstractRoom room,
        Vector2i position,
        int mainZLevel)
    {
        var result = new List<(Vector2i, Direction)>();

        if (room.RoomProtoId == null || !_proto.TryIndex(room.RoomProtoId.Value, out var roomProto))
            return result;

        var passways = _dungeon.GetPassways(room.RoomProtoId.Value);

        // Same transform as SpawnRooms / GetWorldPassways, assuming TileSize = 1.
        var center = new Vector2(position.X + room.Size.X / 2f, position.Y + room.Size.Y / 2f);
        var unrotatedOrigin = center - (Vector2)roomProto.Size / 2f;
        var originTfm = Matrix3Helpers.CreateTranslation(unrotatedOrigin.X, unrotatedOrigin.Y);
        var roomTfm = Matrix3Helpers.CreateTransform((Vector2)roomProto.Size / 2f, room.Rotation);
        var tfm = Matrix3x2.Multiply(roomTfm, originTfm);

        var roomCenter = new Vector2(
            roomProto.Offset.X + roomProto.Size.X / 2f,
            roomProto.Offset.Y + roomProto.Size.Y / 2f);
        var tileOffset = -roomCenter + new Vector2(0.5f, 0.5f);

        foreach (var pw in passways)
        {
            if (pw.ZLevel != mainZLevel)
                continue;

            var localIdx = new Vector2(
                pw.TilePosition.X + roomProto.Offset.X,
                pw.TilePosition.Y + roomProto.Offset.Y);
            var worldPos = Vector2.Transform(localIdx + tileOffset, tfm).Floored();
            var rotatedDir = (pw.Direction.ToAngle() + room.Rotation).GetCardinalDir();

            result.Add((worldPos, rotatedDir));
        }

        return result;
    }

    private static bool IsOppositeCardinal(Direction a, Direction b)
    {
        return a switch
        {
            Direction.East => b == Direction.West,
            Direction.West => b == Direction.East,
            Direction.North => b == Direction.South,
            Direction.South => b == Direction.North,
            _ => false,
        };
    }

    /// <summary>
    /// Checks whether placing a room at (<paramref name="x"/>, <paramref name="y"/>)
    /// with the given <paramref name="size"/> would overlap any existing room.
    /// Uses <paramref name="parentGap"/> for the parent room and
    /// <paramref name="defaultGap"/> for all other rooms.
    /// </summary>
    private static bool WouldOverlap(
        List<CEProceduralAbstractRoom> rooms,
        int selfIndex,
        int parentIndex,
        int x,
        int y,
        Vector2i size,
        int parentGap,
        int defaultGap)
    {
        foreach (var other in rooms)
        {
            if (other.Index == selfIndex)
                continue;

            var gap = other.Index == parentIndex ? parentGap : defaultGap;

            // Expanded AABB of the candidate (including gap on all sides).
            var minX = x - gap;
            var minY = y - gap;
            var maxX = x + size.X + gap;
            var maxY = y + size.Y + gap;

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
