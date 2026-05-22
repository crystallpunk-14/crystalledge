using System.Numerics;
using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: corridor pathfinding/placement and door spawning between connected rooms.
/// <para>
/// Connection modes:
/// <list type="bullet">
///   <item><b>Wide (both rooms have <see cref="CERoomTypePrototype.SupportsWideConnection"/> = true, gap = 1)</b>:
///     floor tiles at every aligned passway gap — open passage, no doors.</item>
///   <item><b>Door-mode</b> (default):
///     A* corridor from passway to passway, doors at both endpoints.
///     Each door uses the <see cref="CERoomTypePrototype.DoorPrototype"/> of the <em>opposite</em> room,
///     so the player sees the special door before entering the room (e.g. gold door visible from outside the treasury).</item>
/// </list>
/// </para>
/// </summary>
public sealed partial class CEProceduralGeneratorSystem
{
    /// <summary>
    /// Builds corridors and spawns doors for every graph connection.
    /// </summary>
    internal async Task BuildCorridors(
        CEGeneratingProceduralDungeonComponent comp,
        CEProceduralConfig config,
        EntityUid gridUid,
        MapGridComponent grid,
        Random random,
        HashSet<Vector2i> reservedTiles,
        Func<ValueTask> suspend)
    {
        // Index rooms.
        var roomByIndex = new Dictionary<int, CEProceduralAbstractRoom>();
        foreach (var room in comp.Rooms)
        {
            roomByIndex[room.Index] = room;
        }

        // Resolve the corridor tile.
        var tileDef = _tileDef[config.CorridorTile];
        var corridorTile = new Tile(tileDef.TileId);

        var mainZLevel = config.MainZLevel;

        // --- Pre-resolve room type prototypes for door and connection-mode lookups. ---
        var roomTypeProtos = new Dictionary<int, CERoomTypePrototype?>();
        foreach (var room in comp.Rooms)
        {
            if (!_proto.Resolve(room.RoomType, out var roomType))
                continue;

            roomTypeProtos[room.Index] = roomType;
        }

        // --- Process connections ---
        // Build an expanded obstacle set: room tiles + 1-tile cardinal buffer.
        // This prevents corridors from running along or diagonally touching room walls.
        var roomBuffer = new HashSet<Vector2i>(reservedTiles);
        foreach (var tile in reservedTiles)
        {
            roomBuffer.Add(tile + new Vector2i(1, 0));
            roomBuffer.Add(tile + new Vector2i(-1, 0));
            roomBuffer.Add(tile + new Vector2i(0, 1));
            roomBuffer.Add(tile + new Vector2i(0, -1));
            roomBuffer.Add(tile + new Vector2i(1, 1));
            roomBuffer.Add(tile + new Vector2i(1, -1));
            roomBuffer.Add(tile + new Vector2i(-1, 1));
            roomBuffer.Add(tile + new Vector2i(-1, -1));
        }

        // Collect tile positions and door placements in batches.
        // Each door placement records the tile position, rotation and the door prototype to spawn.
        var corridorPositions = new HashSet<Vector2i>();
        var doorPlacements = new List<(Vector2i Pos, Angle Rotation, EntProtoId DoorProto)>();

        var connCounter = 0;
        foreach (var conn in comp.Connections)
        {
            if (connCounter > 0)
                await suspend();
            connCounter++;

            if (!roomByIndex.TryGetValue(conn.RoomA, out var roomA) ||
                !roomByIndex.TryGetValue(conn.RoomB, out var roomB))
                continue;

            var protoA = roomTypeProtos.GetValueOrDefault(conn.RoomA);
            var protoB = roomTypeProtos.GetValueOrDefault(conn.RoomB);

            // Wide connection: both room types explicitly support it and rooms are adjacent.
            var isWideMode = conn.IsClose
                             && (protoA?.SupportsWideConnection ?? false)
                             && (protoB?.SupportsWideConnection ?? false);

            if (isWideMode)
            {
                // Wide-mode: place floor tiles at all aligned passway gaps (no doors).
                var pwA = GetPasswayWorldTiles(roomA, roomA.Position, mainZLevel);
                var pwB = GetPasswayWorldTiles(roomB, roomB.Position, mainZLevel);
                foreach (var (posA, dirA) in pwA)
                {
                    var outsideA = posA + dirA.ToIntVec();
                    foreach (var (posB, dirB) in pwB)
                    {
                        if (IsOppositeCardinal(dirA, dirB) && outsideA == posB + dirB.ToIntVec())
                            corridorPositions.Add(outsideA);
                    }
                }
            }
            else
            {
                // Door-mode: A* corridor with per-room-type doors at the endpoints.
                var doorProtoA = protoA?.DoorPrototype ?? config.DoorPrototype;
                var doorProtoB = protoB?.DoorPrototype ?? config.DoorPrototype;
                await BuildDoorCorridor(
                    roomA,
                    roomB,
                    mainZLevel,
                    random,
                    config.CorridorWander,
                    roomBuffer,
                    corridorPositions,
                    doorPlacements,
                    doorProtoA,
                    doorProtoB,
                    suspend);
            }
        }

        // --- Place corridor tiles ---
        if (corridorPositions.Count > 0)
        {
            var tiles = new List<(Vector2i, Tile)>(corridorPositions.Count);
            foreach (var pos in corridorPositions)
            {
                if (reservedTiles.Contains(pos))
                    continue;

                tiles.Add((pos, corridorTile));
                reservedTiles.Add(pos);
            }

            _maps.SetTiles(gridUid, grid, tiles);
        }

        // --- Spawn doors ---
        foreach (var (pos, rot, doorProto) in doorPlacements)
        {
            var worldPos = new Vector2(pos.X + 0.5f, pos.Y + 0.5f);
            SpawnAttachedTo(
                doorProto,
                new EntityCoordinates(gridUid, worldPos),
                rotation: rot);
        }
    }


    /// <summary>
    /// Finds the best passway pair between two rooms, runs A* to connect them,
    /// and records corridor tile positions + door placements at the endpoints.
    /// Door prototypes are per-room: <paramref name="doorProtoA"/> is placed at the
    /// RoomA end of the corridor; <paramref name="doorProtoB"/> at the RoomB end.
    /// </summary>
    private async Task BuildDoorCorridor(
        CEProceduralAbstractRoom roomA,
        CEProceduralAbstractRoom roomB,
        int mainZLevel,
        Random random,
        float wanderWeight,
        HashSet<Vector2i> obstacles,
        HashSet<Vector2i> corridorPositions,
        List<(Vector2i Pos, Angle Rotation, EntProtoId DoorProto)> doorPlacements,
        EntProtoId doorProtoA,
        EntProtoId doorProtoB,
        Func<ValueTask> suspend)
    {
        var exitsA = GetPasswayWorldTiles(roomA, roomA.Position, mainZLevel);
        var exitsB = GetPasswayWorldTiles(roomB, roomB.Position, mainZLevel);

        if (exitsA.Count == 0 || exitsB.Count == 0)
            return;

        // Find the closest pair of facing exits on the connecting axis.
        var dirAtoB = GridCoordToDirection(roomB.GridCoord - roomA.GridCoord);
        var dirBtoA = GridCoordToDirection(roomA.GridCoord - roomB.GridCoord);

        Vector2i? bestStart = null;
        Vector2i? bestEnd = null;
        var bestDist = int.MaxValue;

        foreach (var (posA, dirA) in exitsA)
        {
            if (dirA != dirAtoB)
                continue;

            var startTile = posA + dirA.ToIntVec();

            foreach (var (posB, dirB) in exitsB)
            {
                if (dirB != dirBtoA)
                    continue;

                var endTile = posB + dirB.ToIntVec();
                var dist = Math.Abs(startTile.X - endTile.X) + Math.Abs(startTile.Y - endTile.Y);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestStart = startTile;
                    bestEnd = endTile;
                }
            }
        }

        if (bestStart == null || bestEnd == null)
            return;

        // A* path between the two outside-of-room tiles.
        // The obstacles set includes a 1-tile buffer around rooms, so corridors
        // won't run along room walls. Start and end are exempt in the pathfinder.
        var path = await FindWanderingPath(
            bestStart.Value,
            bestEnd.Value,
            obstacles,
            random,
            wanderWeight,
            suspend);

        // Record corridor positions.
        foreach (var pos in path)
        {
            corridorPositions.Add(pos);
        }

        // Door at the start (faces toward room A, uses roomB's prototype — the player sees roomB's door when approaching from outside).
        if (path.Count > 0)
        {
            doorPlacements.Add((path[0], dirBtoA.ToAngle(), doorProtoB));
        }

        // Door at the end (faces toward room B, uses roomA's prototype) — only if different tile.
        if (path.Count > 1)
        {
            doorPlacements.Add((path[^1], dirAtoB.ToAngle(), doorProtoA));
        }
    }

    /// <summary>
    /// Weighted A* pathfinding with random wander.
    /// Adds a random cost to each step so the path meanders slightly.
    /// Avoids occupied tiles. Yields periodically to avoid blocking the server.
    /// </summary>
    private static async Task<List<Vector2i>> FindWanderingPath(
        Vector2i start,
        Vector2i end,
        HashSet<Vector2i> obstacles,
        Random random,
        float wanderWeight,
        Func<ValueTask> suspend)
    {
        // Trivial case: start == end.
        if (start == end)
            return [start];

        var openSet = new PriorityQueue<Vector2i, float>();
        var cameFrom = new Dictionary<Vector2i, Vector2i>();
        var gScore = new Dictionary<Vector2i, float> { [start] = 0 };

        openSet.Enqueue(start, 0);

        var cardinals = new Vector2i[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        var astarCounter = 0;

        while (openSet.Count > 0)
        {
            if (++astarCounter % 200 == 0)
                await suspend();

            var current = openSet.Dequeue();

            if (current == end)
            {
                var path = new List<Vector2i>();
                var c = current;
                while (cameFrom.ContainsKey(c))
                {
                    path.Add(c);
                    c = cameFrom[c];
                }

                path.Add(start);
                path.Reverse();
                return path;
            }

            var currentG = gScore.GetValueOrDefault(current, float.MaxValue);

            foreach (var dir in cardinals)
            {
                var neighbor = current + dir;

                // Can't walk through obstacles, but start and end tiles are always reachable.
                if (neighbor != start && neighbor != end && obstacles.Contains(neighbor))
                    continue;

                var tentativeG = currentG + 1f + (float)(random.NextDouble() * wanderWeight);
                var existingG = gScore.GetValueOrDefault(neighbor, float.MaxValue);

                if (tentativeG >= existingG)
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                var h = Math.Abs(neighbor.X - end.X) + Math.Abs(neighbor.Y - end.Y);
                openSet.Enqueue(neighbor, tentativeG + h);
            }
        }

        // No path found — return direct line as fallback.
        return [start, end];
    }
}
