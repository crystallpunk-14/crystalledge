using System.Numerics;
using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: corridor pathfinding and placement between connected rooms.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem
{
    /// <summary>
    /// For each graph connection, finds the closest pair of facing passway markers
    /// between the two rooms and lays a slightly wandering A* corridor of tiles between them.
    /// Only empty tiles are filled — existing room tiles are never overwritten.
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
            roomByIndex[room.Index] = room;

        // Resolve the corridor tile.
        var tileDef = _tileDef[config.CorridorTile];
        var corridorTile = new Tile(tileDef.TileId);

        var mainZLevel = config.MainZLevel;

        // Collect all corridor positions first, then place in a single batch.
        // A* only avoids room-reserved tiles, NOT other corridors.
        // This ensures corridors can freely path through 1-tile gaps between rooms
        // without being blocked by previously laid corridors.
        var corridorPositions = new HashSet<Vector2i>();

        var corridorCounter = 0;
        foreach (var conn in comp.Connections)
        {
            // Yield every connection — exit matching + A* per connection.
            if (corridorCounter > 0)
                await suspend();
            corridorCounter++;
            if (!roomByIndex.TryGetValue(conn.RoomA, out var roomA) ||
                !roomByIndex.TryGetValue(conn.RoomB, out var roomB))
                continue;

            // Compute world-space exit positions for each room.
            var exitsA = GetWorldPassways(roomA, grid, mainZLevel);
            var exitsB = GetWorldPassways(roomB, grid, mainZLevel);

            if (exitsA.Count == 0 || exitsB.Count == 0)
                continue;

            // Find the closest pair of facing exits.
            // Exit A must face toward room B and vice versa.
            var dirAtoB = GridCoordToDirection(roomB.GridCoord - roomA.GridCoord);
            var dirBtoA = GridCoordToDirection(roomA.GridCoord - roomB.GridCoord);

            Vector2i? bestStartTile = null;
            Vector2i? bestEndTile = null;
            var bestDist = int.MaxValue;

            foreach (var (posA, dirA) in exitsA)
            {
                if (dirA != dirAtoB)
                    continue;

                // The corridor starts one tile outside the room boundary.
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
                        bestStartTile = startTile;
                        bestEndTile = endTile;
                    }
                }
            }

            if (bestStartTile == null || bestEndTile == null)
                continue;

            // Run weighted A* with wandering. Only room tiles are obstacles.
            var path = await FindWanderingPath(bestStartTile.Value, bestEndTile.Value, reservedTiles, random, config.CorridorWander, suspend);

            // Accumulate corridor positions (skip tiles that overlap rooms).
            foreach (var pos in path)
            {
                if (!reservedTiles.Contains(pos))
                    corridorPositions.Add(pos);
            }
        }

        // Place all corridor tiles in a single batch and add to reserved set.
        if (corridorPositions.Count > 0)
        {
            var tiles = new List<(Vector2i, Tile)>(corridorPositions.Count);
            foreach (var pos in corridorPositions)
            {
                tiles.Add((pos, corridorTile));
                reservedTiles.Add(pos);
            }

            _maps.SetTiles(gridUid, grid, tiles);
        }
    }

    /// <summary>
    /// Gets the world-space tile positions and rotated directions of all passway markers
    /// in the given abstract room.
    /// </summary>
    private List<(Vector2i WorldTilePos, Direction FacingDir)> GetWorldPassways(
        CEProceduralAbstractRoom room,
        MapGridComponent grid,
        int mainZLevel)
    {
        var result = new List<(Vector2i, Direction)>();

        if (room.RoomProtoId == null || !_proto.TryIndex(room.RoomProtoId.Value, out var roomProto))
            return result;

        var passways = _dungeon.GetPassways(room.RoomProtoId.Value);

        // Build the same transform as room spawning.
        var center = new Vector2(room.Position.X + room.Size.X / 2f, room.Position.Y + room.Size.Y / 2f);
        var unrotatedOrigin = center - (Vector2)roomProto.Size / 2f;
        var originTfm = Matrix3Helpers.CreateTranslation(unrotatedOrigin.X, unrotatedOrigin.Y);
        var roomTfm = Matrix3Helpers.CreateTransform((Vector2)roomProto.Size / 2f, room.Rotation);
        var tfm = Matrix3x2.Multiply(roomTfm, originTfm);

        var roomCenter = (roomProto.Offset + roomProto.Size / 2f) * grid.TileSize;
        var tileOffset = -roomCenter + grid.TileSizeHalfVector;

        foreach (var pw in passways)
        {
            // Only consider passways on the main z-level.
            if (pw.ZLevel != mainZLevel)
                continue;

            var localIdx = new Vector2i(pw.TilePosition.X + roomProto.Offset.X, pw.TilePosition.Y + roomProto.Offset.Y);
            var worldPos = Vector2.Transform(localIdx + tileOffset, tfm).Floored();
            var rotatedDir = (pw.Direction.ToAngle() + room.Rotation).GetCardinalDir();

            result.Add((worldPos, rotatedDir));
        }

        return result;
    }

    /// <summary>
    /// Weighted A* pathfinding with random wander.
    /// Adds a random cost to each step so the path meanders slightly.
    /// Avoids occupied tiles. Yields periodically to avoid blocking the server.
    /// </summary>
    private static async Task<List<Vector2i>> FindWanderingPath(
        Vector2i start,
        Vector2i end,
        HashSet<Vector2i> occupied,
        Random random,
        float wanderWeight,
        Func<ValueTask> suspend)
    {
        // A* with weighted heuristic.
        var openSet = new PriorityQueue<Vector2i, float>();
        var cameFrom = new Dictionary<Vector2i, Vector2i>();
        var gScore = new Dictionary<Vector2i, float> { [start] = 0 };

        openSet.Enqueue(start, 0);

        var cardinals = new Vector2i[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        var astarCounter = 0;

        while (openSet.Count > 0)
        {
            // Yield every 200 A* iterations — safety valve for rare long paths.
            if (++astarCounter % 200 == 0)
                await suspend();
            var current = openSet.Dequeue();

            if (current == end)
            {
                // Reconstruct path.
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

                // Can't walk through occupied tiles (rooms), but the end tile is always reachable.
                if (neighbor != end && occupied.Contains(neighbor))
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
