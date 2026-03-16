using System.Numerics;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Configuration for the procedural dungeon generator.
/// </summary>
public sealed partial class CEProceduralConfig : CEDungeonGeneratorConfigBase<CEProceduralConfig>
{
    /// <summary>
    /// Although the generator works with z-levels, only one of these z-levels is "playable,"
    /// while the rest are purely decorative.
    /// We specify which level is the main one so that all the main generation takes place on that level.
    /// </summary>
    [DataField]
    public int MainZLevel = 1;

    [DataField]
    public int MaxRoomSize = 20;

    [DataField]
    public CEProceduralRoomPack GeneralRooms = new();

    [DataField]
    public MinMax GeneralCount = new(30, 50);

    /// <summary>
    /// Pack used for the exit room (placed at grid origin).
    /// </summary>
    [DataField]
    public CEProceduralRoomPack ExitRoom = new();

    /// <summary>
    /// Pack used for entrance rooms (dead-ends, maximally far apart).
    /// </summary>
    [DataField]
    public CEProceduralRoomPack EntranceRooms = new();

    [DataField]
    public MinMax EntranceCount = new(2, 2);

    /// <summary>
    /// Pack used for blessing/treasure rooms (dead-ends, maximally far apart).
    /// </summary>
    [DataField]
    public CEProceduralRoomPack BlessingRooms = new();

    [DataField]
    public MinMax BlessingCount = new(2, 2);

    /// <summary>
    /// Pack used for dead-end rooms (remaining dead-ends after entrances and blessings).
    /// </summary>
    [DataField]
    public CEProceduralRoomPack DeadEndRooms = new();

    /// <summary>
    /// Shared components applied to every z-level map in the dungeon's z-network
    /// (e.g. MapAtmosphere, MapLight, CEZLevelMapRoof).
    /// </summary>
    [DataField]
    public ComponentRegistry Components = new();

    /// <summary>
    /// How much the corridor A* path is allowed to wander (0 = straight, higher = more winding).
    /// Added as a random cost multiplier to each pathfinding step.
    /// </summary>
    [DataField]
    public float CorridorWander = 3f;

    /// <summary>
    /// Tile prototype used for corridors between rooms.
    /// </summary>
    [DataField]
    public ProtoId<ContentTileDefinition> CorridorTile = "CEStone";

    /// <summary>
    /// Entity prototype spawned as a wall around the perimeter of all rooms and corridors.
    /// Placed on every z-level at positions adjacent (including diagonals) to reserved tiles.
    /// </summary>
    [DataField]
    public EntProtoId WallPrototype = "CEWallStoneBrick";
}

[DataDefinition]
public sealed partial class CEProceduralRoomPack
{
    /// <summary>
    /// Filtering rooms that are suitable for this pack
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;
}

/// <summary>
/// Procedural dungeon generator. Builds an abstract room graph on a logical grid
/// then (in future steps) places actual rooms on the map.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem : CEDungeonGeneratorSystem<CEProceduralConfig>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly CEDungeonSystem _dungeon = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;

    /// <summary>
    /// Cached set of reserved (occupied) tile positions.
    /// Re-used across generation steps to avoid repeated allocations.
    /// Cleared at the start of each <see cref="Generate"/> call.
    /// </summary>
    private HashSet<Vector2i> _reservedTiles = new();

    /// <summary>
    /// Cardinal directions on the logical grid: right, left, up, down.
    /// </summary>
    private static readonly Vector2i[] Directions =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];



    protected override void Generate(ref CEDungeonGenerateEvent<CEProceduralConfig> args)
    {
        var config = args.Config;

        // Determine how many rooms to generate.
        var targetCount = _random.Next(config.GeneralCount.Min, config.GeneralCount.Max + 1);
        if (targetCount <= 0)
            return;

        // Create a new map for this dungeon.
        var mapUid = _maps.CreateMap(out var mapId);

        // Build the abstract room graph.
        var comp = AddComp<CEGeneratingProceduralDungeonComponent>(mapUid);

        BuildRoomGraph(comp, config.MaxRoomSize, targetCount);

        // Assign room types before selecting real prototypes.
        AssignRoomTypes(comp, config);

        // Assign real room prototypes, apply rotation, resize and randomize position.
        AssignRealRooms(comp, config);

        // Compact: slide rooms toward their parent (BFS order), maintaining 1-tile gap.
        CompactRooms(comp);

        // Create z-network so 3D rooms can be spawned across z-levels.
        var network = _zLevels.CreateZNetwork(config.Components);

        // Determine the maximum room height to know how many z-levels we need.
        var maxHeight = 1;
        foreach (var room in comp.Rooms)
        {
            if (room.RoomProtoId == null)
                continue;

            if (_proto.TryIndex(room.RoomProtoId.Value, out var rp) && rp.Height > maxHeight)
                maxHeight = rp.Height;
        }

        // Create a map for each required z-level and register them in the network.
        // Rooms always start at depth 0; MainZLevel is metadata for post-generation logic.
        var mapsByDepth = new Dictionary<EntityUid, int>
        {
            { mapUid, 0 }
        };

        for (var zOffset = 1; zOffset < maxHeight; zOffset++)
        {
            var extraMapUid = _maps.CreateMap(out _);
            EnsureComp<MapGridComponent>(extraMapUid);
            mapsByDepth[extraMapUid] = zOffset;
        }

        _zLevels.TryAddMapsIntoZNetwork(network, mapsByDepth);

        // Ensure the map has a grid for tile/entity placement.
        var grid = EnsureComp<MapGridComponent>(mapUid);

        // Clear and re-use the cached reserved tile set.
        _reservedTiles.Clear();

        // Spawn each room's 3D prototype onto the grid.
        var rng = new Random(_random.Next());
        SpawnRooms(comp, mapUid, grid, rng, _reservedTiles);

        // Resolve the grid at MainZLevel for corridor placement.
        // The base grid (mapUid) is at depth 0; corridors go on the main playable z-level.
        var corridorGridUid = mapUid;
        var corridorGrid = grid;

        if (config.MainZLevel != 0)
        {
            if (_zLevels.TryMapOffset((mapUid, EnsureComp<CEZLevelMapComponent>(mapUid)), config.MainZLevel, out var mainLevelMap))
            {
                corridorGridUid = mainLevelMap.Value;
                corridorGrid = EnsureComp<MapGridComponent>(corridorGridUid);
            }
            else
            {
                Log.Warning($"CEProceduralGeneratorSystem: could not resolve MainZLevel {config.MainZLevel} for corridors.");
            }
        }

        // Build corridors between connected rooms.
        BuildCorridors(comp, config, corridorGridUid, corridorGrid, rng, _reservedTiles);

        // Place walls around the perimeter of all rooms and corridors on every z-level.
        PlaceWalls(config, mapUid, mapsByDepth, _reservedTiles);

        Dirty(mapUid, comp);

        // Report results.
        args.MapUid = mapUid;
        args.MapId = mapId;
        args.Handled = true;
    }

    /// <summary>
    /// 8-directional offsets (cardinals + diagonals) for perimeter detection.
    /// </summary>
    private static readonly Vector2i[] AllNeighbors =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    ];

    /// <summary>
    /// Places wall entities around the perimeter of all reserved (occupied) tiles.
    /// A wall is placed at every neighbouring position (8-directional, including diagonals)
    /// that is not itself a reserved tile. Walls are spawned on every z-level in the network.
    /// </summary>
    private void PlaceWalls(
        CEProceduralConfig config,
        EntityUid baseMapUid,
        Dictionary<EntityUid, int> mapsByDepth,
        HashSet<Vector2i> reservedTiles)
    {
        // Compute wall positions: neighbours of reserved tiles that are not occupied.
        var wallPositions = new HashSet<Vector2i>();

        foreach (var tile in reservedTiles)
        {
            foreach (var offset in AllNeighbors)
            {
                var neighbor = tile + offset;
                if (!reservedTiles.Contains(neighbor))
                    wallPositions.Add(neighbor);
            }
        }

        if (wallPositions.Count == 0)
            return;

        // Spawn walls on every z-level.
        foreach (var (levelMapUid, _) in mapsByDepth)
        {
            foreach (var pos in wallPositions)
            {
                // Tile center = (tileX + 0.5, tileY + 0.5).
                var worldPos = new Vector2(pos.X + 0.5f, pos.Y + 0.5f);
                Spawn(config.WallPrototype, new EntityCoordinates(levelMapUid, worldPos));
            }
        }
    }

    /// <summary>
    /// Spawns a 3D room prototype for each abstract room that has a valid <see cref="CEProceduralAbstractRoom.RoomProtoId"/>.
    /// The room is placed at the room's <see cref="CEProceduralAbstractRoom.Position"/> with
    /// the pre-computed <see cref="CEProceduralAbstractRoom.Rotation"/>.
    /// </summary>
    private void SpawnRooms(
        CEGeneratingProceduralDungeonComponent comp,
        EntityUid gridUid,
        MapGridComponent grid,
        Random random,
        HashSet<Vector2i> reservedTiles)
    {

        foreach (var room in comp.Rooms)
        {
            if (room.RoomProtoId == null)
                continue;

            if (!_proto.TryIndex(room.RoomProtoId.Value, out var roomProto))
            {
                Log.Warning($"CEProceduralGeneratorSystem: unknown room prototype '{room.RoomProtoId}'.");
                continue;
            }

            // Build the transform: translate to the unrotated room origin, then rotate.
            // room.Position is the top-left of the EFFECTIVE (rotated) bounding box.
            // The transform expects the top-left of the UNROTATED prototype.
            // Both share the same center:
            //   room.Position + effectiveSize/2 == unrotatedOrigin + protoSize/2
            var center = new Vector2(
                room.Position.X + room.Size.X / 2f,
                room.Position.Y + room.Size.Y / 2f);
            var unrotatedOrigin = center - (Vector2)roomProto.Size / 2f;

            var originTransform = Matrix3Helpers.CreateTranslation(unrotatedOrigin.X, unrotatedOrigin.Y);
            var roomTransform = Matrix3Helpers.CreateTransform((Vector2)roomProto.Size / 2f, room.Rotation);
            var finalTransform = Matrix3x2.Multiply(roomTransform, originTransform);

            if (!_dungeon.TrySpawn3DRoom(gridUid, grid, finalTransform, roomProto, reservedTiles))
            {
                Log.Warning($"CEProceduralGeneratorSystem: failed to spawn room {room.Index} (proto '{room.RoomProtoId}').");
                continue;
            }

            // After the room is fully spawned, mark its tile positions as reserved
            // so future rooms don't overwrite them.
            var roomCenter = (roomProto.Offset + roomProto.Size / 2f) * grid.TileSize;
            var tileOffset = -roomCenter + grid.TileSizeHalfVector;

            for (var x = 0; x < roomProto.Size.X; x++)
            {
                for (var y = 0; y < roomProto.Size.Y; y++)
                {
                    var indices = new Vector2i(x + roomProto.Offset.X, y + roomProto.Offset.Y);
                    var tilePos = Vector2.Transform(indices + tileOffset, finalTransform);
                    reservedTiles.Add(tilePos.Floored());
                }
            }
        }
    }

    /// <summary>
    /// For each graph connection, finds the closest pair of facing passway markers
    /// between the two rooms and lays a slightly wandering A* corridor of tiles between them.
    /// Only empty tiles are filled — existing room tiles are never overwritten.
    /// </summary>
    private void BuildCorridors(
        CEGeneratingProceduralDungeonComponent comp,
        CEProceduralConfig config,
        EntityUid gridUid,
        MapGridComponent grid,
        Random random,
        HashSet<Vector2i> reservedTiles)
    {
        // Index rooms.
        var roomByIndex = new Dictionary<int, CEProceduralAbstractRoom>();
        foreach (var room in comp.Rooms)
            roomByIndex[room.Index] = room;

        // Resolve the corridor tile.
        var tileDef = _tileDef[config.CorridorTile];
        var corridorTile = new Tile(tileDef.TileId);

        var mainZLevel = config.MainZLevel;

        foreach (var conn in comp.Connections)
        {
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

            // Run weighted A* with wandering.
            var path = FindWanderingPath(bestStartTile.Value, bestEndTile.Value, reservedTiles, random, config.CorridorWander);

            // Place corridor tiles along the path.
            var tiles = new List<(Vector2i, Tile)>();
            foreach (var pos in path)
            {
                if (reservedTiles.Contains(pos))
                    continue;

                tiles.Add((pos, corridorTile));
                reservedTiles.Add(pos);
            }

            if (tiles.Count > 0)
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
    /// Avoids occupied tiles.
    /// </summary>
    private static List<Vector2i> FindWanderingPath(
        Vector2i start,
        Vector2i end,
        HashSet<Vector2i> occupied,
        Random random,
        float wanderWeight)
    {
        // A* with weighted heuristic.
        var openSet = new PriorityQueue<Vector2i, float>();
        var cameFrom = new Dictionary<Vector2i, Vector2i>();
        var gScore = new Dictionary<Vector2i, float> { [start] = 0 };

        openSet.Enqueue(start, 0);

        var cardinals = new Vector2i[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        while (openSet.Count > 0)
        {
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
    private void BuildRoomGraph(
        CEGeneratingProceduralDungeonComponent comp,
        int maxRoomSize,
        int targetCount)
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

        while (comp.Rooms.Count < targetCount && frontier.Count > 0)
        {
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

    /// <summary>
    /// For each abstract room, selects a random real <see cref="CEDungeonRoom3DPrototype"/>
    /// that fits within MaxRoomSize, chooses a rotation that satisfies the required exit
    /// directions (based on neighbour connections), shrinks the abstract room to the
    /// real room's size, and randomizes its position within the original grid cell.
    /// Uses the whitelist from the room's type-specific pack.
    /// </summary>
    private void AssignRealRooms(CEGeneratingProceduralDungeonComponent comp, CEProceduralConfig config)
    {
        var maxSize = config.MaxRoomSize;
        var step = maxSize + 1;
        var random = new Random(_random.Next());
        var maxSizeVec = new Vector2i(maxSize, maxSize);

        // Ensure the passway cache is built before we start checking exits.
        _dungeon.EnsureRoomPasswayCache();

        // Build a map of required exit directions per room index.
        // For each room, the required exits are the directions toward its graph neighbours.
        var requiredExits = BuildRequiredExitsMap(comp);

        // Candidate rotations (0°, 90°, 180°, 270°).
        var candidateRotations = new[] { Angle.Zero, new Angle(Math.PI / 2), new Angle(Math.PI), new Angle(3 * Math.PI / 2) };

        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            var room = comp.Rooms[i];

            // Pick the whitelist based on the room's assigned type.
            var pack = GetPackForType(config, room.RoomType);

            // Determine required exit directions for this room.
            var required = requiredExits.GetValueOrDefault(room.Index) ?? new HashSet<Direction>();

            // Try multiple times to find a valid prototype + rotation combo.
            const int maxAttempts = 50;
            CEDungeonRoom3DPrototype? roomProto = null;
            var chosenRotation = Angle.Zero;
            var found = false;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = _dungeon.GetRoomPrototype(
                    random,
                    pack.Whitelist,
                    maxSize: maxSizeVec);

                if (candidate == null)
                    break;

                // If no exits are required (isolated room), accept any room.
                if (required.Count == 0)
                {
                    roomProto = candidate;
                    chosenRotation = _dungeon.GetRoomRotation(candidate, random);
                    found = true;
                    break;
                }

                // Try each of the 4 cardinal rotations to see if one satisfies all required exits.
                // Shuffle the order so results are not biased toward 0°.
                ShuffleArray(candidateRotations, random);

                foreach (var rot in candidateRotations)
                {
                    if (_dungeon.HasRequiredExits(candidate.ID, rot, required))
                    {
                        roomProto = candidate;
                        chosenRotation = rot;
                        found = true;
                        break;
                    }
                }

                if (found)
                    break;
            }

            if (roomProto == null)
            {
                Log.Warning($"CEProceduralGeneratorSystem: no matching room prototype found for abstract room #{i} (type={room.RoomType}).");
                continue;
            }

            room.RoomProtoId = roomProto.ID;
            room.Rotation = chosenRotation;

            // Calculate effective size after rotation.
            // 90 / 270 degrees swap width and height.
            var isRotated90 = Math.Abs(room.Rotation.Theta - Math.PI / 2) < 0.01
                              || Math.Abs(room.Rotation.Theta - 3 * Math.PI / 2) < 0.01;

            var effectiveSize = isRotated90
                ? new Vector2i(roomProto.Size.Y, roomProto.Size.X)
                : roomProto.Size;

            // Shrink abstract room to match the real room's effective size.
            room.Size = effectiveSize;

            // Randomize position within the original grid cell.
            // The cell origin is gridCoord * step and has maxSize × maxSize space.
            var cellOrigin = new Vector2i(room.GridCoord.X * step, room.GridCoord.Y * step);
            var slack = new Vector2i(
                Math.Max(0, maxSize - effectiveSize.X),
                Math.Max(0, maxSize - effectiveSize.Y));

            var offsetX = slack.X > 0 ? random.Next(slack.X + 1) : 0;
            var offsetY = slack.Y > 0 ? random.Next(slack.Y + 1) : 0;

            room.Position = new Vector2i(cellOrigin.X + offsetX, cellOrigin.Y + offsetY);
        }
    }

    /// <summary>
    /// Builds a map from room index to the set of cardinal directions where the room
    /// must have exits (toward its graph neighbours).
    /// </summary>
    private static Dictionary<int, HashSet<Direction>> BuildRequiredExitsMap(
        CEGeneratingProceduralDungeonComponent comp)
    {
        // Index rooms by their index for GridCoord lookup.
        var roomByIndex = new Dictionary<int, CEProceduralAbstractRoom>();
        foreach (var room in comp.Rooms)
            roomByIndex[room.Index] = room;

        var result = new Dictionary<int, HashSet<Direction>>();

        foreach (var conn in comp.Connections)
        {
            if (!roomByIndex.TryGetValue(conn.RoomA, out var roomA) ||
                !roomByIndex.TryGetValue(conn.RoomB, out var roomB))
                continue;

            var dirAtoB = GridCoordToDirection(roomB.GridCoord - roomA.GridCoord);
            var dirBtoA = GridCoordToDirection(roomA.GridCoord - roomB.GridCoord);

            if (dirAtoB != Direction.Invalid)
            {
                if (!result.TryGetValue(conn.RoomA, out var setA))
                {
                    setA = new HashSet<Direction>();
                    result[conn.RoomA] = setA;
                }
                setA.Add(dirAtoB);
            }

            if (dirBtoA != Direction.Invalid)
            {
                if (!result.TryGetValue(conn.RoomB, out var setB))
                {
                    setB = new HashSet<Direction>();
                    result[conn.RoomB] = setB;
                }
                setB.Add(dirBtoA);
            }
        }

        return result;
    }

    /// <summary>
    /// Converts a grid-coordinate delta (adjacent cell) to a cardinal direction.
    /// </summary>
    private static Direction GridCoordToDirection(Vector2i delta)
    {
        return delta switch
        {
            { X: > 0 } => Direction.East,
            { X: < 0 } => Direction.West,
            { Y: > 0 } => Direction.North,
            { Y: < 0 } => Direction.South,
            _ => Direction.Invalid,
        };
    }

    /// <summary>
    /// Fisher–Yates shuffle for a small array.
    /// </summary>
    private static void ShuffleArray<T>(T[] array, Random random)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    /// <summary>
    /// Returns the <see cref="CEProceduralRoomPack"/> matching the given room type.
    /// </summary>
    private static CEProceduralRoomPack GetPackForType(CEProceduralConfig config, CEProceduralRoomType type)
    {
        return type switch
        {
            CEProceduralRoomType.Exit => config.ExitRoom,
            CEProceduralRoomType.Entrance => config.EntranceRooms,
            CEProceduralRoomType.Blessing => config.BlessingRooms,
            CEProceduralRoomType.DeadEnd => config.DeadEndRooms,
            _ => config.GeneralRooms,
        };
    }

    /// <summary>
    /// Assigns special room types after the graph is built.
    /// <list type="bullet">
    ///   <item>Exit: room at grid (0,0).</item>
    ///   <item>Entrances: dead-ends (1 connection), picked maximally far apart.</item>
    ///   <item>Blessings: remaining dead-ends, picked maximally far apart.</item>
    ///   <item>All other rooms remain General.</item>
    /// </list>
    /// </summary>
    private void AssignRoomTypes(CEGeneratingProceduralDungeonComponent comp, CEProceduralConfig config)
    {
        // Count connections per room.
        var connectionCount = new Dictionary<int, int>();
        foreach (var conn in comp.Connections)
        {
            connectionCount[conn.RoomA] = connectionCount.GetValueOrDefault(conn.RoomA) + 1;
            connectionCount[conn.RoomB] = connectionCount.GetValueOrDefault(conn.RoomB) + 1;
        }

        // 1. Exit at (0, 0).
        foreach (var room in comp.Rooms)
        {
            if (room.GridCoord == Vector2i.Zero)
            {
                room.RoomType = CEProceduralRoomType.Exit;
                break;
            }
        }

        // Collect dead-ends (rooms with exactly 1 connection), excluding the exit.
        var deadEnds = new List<CEProceduralAbstractRoom>();
        foreach (var room in comp.Rooms)
        {
            if (room.RoomType != CEProceduralRoomType.General)
                continue;

            if (connectionCount.GetValueOrDefault(room.Index) == 1)
                deadEnds.Add(room);
        }

        // 2. Entrances: pick from dead-ends, maximally far apart.
        var entranceCount = _random.Next(
            config.EntranceCount.Min,
            config.EntranceCount.Max + 1);
        PickFarApart(deadEnds, CEProceduralRoomType.Entrance, entranceCount);

        // Remove assigned rooms from dead-end pool.
        deadEnds.RemoveAll(r => r.RoomType != CEProceduralRoomType.General);

        // 3. Blessings: pick from remaining dead-ends, maximally far apart.
        var blessingCount = _random.Next(
            config.BlessingCount.Min,
            config.BlessingCount.Max + 1);
        PickFarApart(deadEnds, CEProceduralRoomType.Blessing, blessingCount);

        // Remove assigned rooms from dead-end pool.
        deadEnds.RemoveAll(r => r.RoomType != CEProceduralRoomType.General);

        // 4. Dead-ends: all remaining dead-end rooms get the DeadEnd type.
        foreach (var room in deadEnds)
        {
            room.RoomType = CEProceduralRoomType.DeadEnd;
        }
    }

    /// <summary>
    /// Greedily picks rooms from <paramref name="candidates"/> that are maximally far apart
    /// from already-picked rooms and assigns them the given <paramref name="type"/>.
    /// Uses grid-coordinate Manhattan distance.
    /// </summary>
    private static void PickFarApart(
        List<CEProceduralAbstractRoom> candidates,
        CEProceduralRoomType type,
        int count)
    {
        if (count <= 0 || candidates.Count == 0)
            return;

        var picked = new List<CEProceduralAbstractRoom>();

        for (var n = 0; n < count && candidates.Count > 0; n++)
        {
            CEProceduralAbstractRoom? best = null;
            var bestMinDist = -1;

            foreach (var candidate in candidates)
            {
                if (candidate.RoomType != CEProceduralRoomType.General)
                    continue;

                // Minimum Manhattan distance to all already picked rooms.
                var minDist = int.MaxValue;
                foreach (var p in picked)
                {
                    var dist = Math.Abs(candidate.GridCoord.X - p.GridCoord.X)
                               + Math.Abs(candidate.GridCoord.Y - p.GridCoord.Y);
                    if (dist < minDist)
                        minDist = dist;
                }

                // First pick: use MaxValue so any candidate wins.
                if (picked.Count == 0)
                    minDist = int.MaxValue;

                if (minDist > bestMinDist)
                {
                    bestMinDist = minDist;
                    best = candidate;
                }
            }

            if (best == null)
                break;

            best.RoomType = type;
            picked.Add(best);
        }
    }

    /// <summary>
    /// Slides every room toward its parent room in BFS order from the root.
    /// The root room (index 0 / exit) stays in place. Each child is slid toward
    /// the centre of its parent as close as possible while keeping a 1-tile gap
    /// to every other room. This prevents connection lines from passing through
    /// unrelated rooms.
    /// </summary>
    private static void CompactRooms(CEGeneratingProceduralDungeonComponent comp)
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
        foreach (var roomIdx in bfsOrder)
        {
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
