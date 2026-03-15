using System.Numerics;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Procedural;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Whitelist;
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

    /// <summary>
    /// Pack used for blessing/treasure rooms (dead-ends, maximally far apart).
    /// </summary>
    [DataField]
    public CEProceduralRoomPack BlessingRooms = new();

    /// <summary>
    /// Shared components applied to every z-level map in the dungeon's z-network
    /// (e.g. MapAtmosphere, MapLight, CEZLevelMapRoof).
    /// </summary>
    [DataField]
    public ComponentRegistry Components = new();
}

[DataDefinition]
public sealed partial class CEProceduralRoomPack
{
    /// <summary>
    /// Filtering rooms that are suitable for this pack
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// the number of rooms generated in this pack
    /// </summary>
    [DataField]
    public MinMax RoomCount = new(1, 1);
}

/// <summary>
/// Procedural dungeon generator. Builds an abstract room graph on a logical grid
/// then (in future steps) places actual rooms on the map.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem : CEDungeonGeneratorSystem<CEProceduralConfig>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly CEDungeonSystem _dungeon = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;

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

    /// <summary>
    /// Maximum number of placement attempts before giving up.
    /// Prevents infinite loops when the grid is too congested.
    /// </summary>
    private const int MaxPlacementAttempts = 1000;

    protected override void Generate(ref CEDungeonGenerateEvent<CEProceduralConfig> args)
    {
        var config = args.Config;

        // Determine how many rooms to generate.
        var targetCount = _random.Next(config.GeneralRooms.RoomCount.Min, config.GeneralRooms.RoomCount.Max + 1);
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

        // Spawn each room's 3D prototype onto the grid.
        var rng = new Random(_random.Next());
        SpawnRooms(comp, mapUid, grid, rng);

        Dirty(mapUid, comp);

        // Report results.
        args.MapUid = mapUid;
        args.MapId = mapId;
        args.Handled = true;
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
        Random random)
    {
        HashSet<Vector2i>? reservedTiles = null;

        foreach (var room in comp.Rooms)
        {
            if (room.RoomProtoId == null)
                continue;

            if (!_proto.TryIndex(room.RoomProtoId.Value, out var roomProto))
            {
                Log.Warning($"CEProceduralGeneratorSystem: unknown room prototype '{room.RoomProtoId}'.");
                continue;
            }

            // Build the transform: translate to room position, then apply rotation around room centre.
            var originTransform = Matrix3Helpers.CreateTranslation(room.Position.X, room.Position.Y);
            var roomTransform = Matrix3Helpers.CreateTransform((Vector2)roomProto.Size / 2f, room.Rotation);
            var finalTransform = Matrix3x2.Multiply(roomTransform, originTransform);

            if (!_dungeon.TrySpawn3DRoom(gridUid, grid, finalTransform, roomProto, reservedTiles))
            {
                Log.Warning($"CEProceduralGeneratorSystem: failed to spawn room {room.Index} (proto '{room.RoomProtoId}').");
            }
        }
    }

    /// <summary>
    /// Builds the abstract room graph on a logical 2D grid.
    /// Each room occupies exactly one grid cell. The world-tile position is
    /// <c>gridCoord * (maxRoomSize + 1)</c>, where the +1 accounts for
    /// a 1-tile gap between adjacent rooms.
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

        var attempts = 0;

        while (comp.Rooms.Count < targetCount && attempts < MaxPlacementAttempts)
        {
            attempts++;

            // Pick a random existing room to branch from.
            var parentIdx = _random.Next(comp.Rooms.Count);
            var parent = comp.Rooms[parentIdx];

            // Pick a random cardinal direction.
            var dir = _random.Pick(Directions);
            var newGridCoord = parent.GridCoord + dir;

            // Check if spot is already taken.
            if (occupied.Contains(newGridCoord))
                continue;

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
                RoomA = parentIdx,
                RoomB = newRoom.Index,
            });
        }
    }

    /// <summary>
    /// For each abstract room, selects a random real <see cref="CEDungeonRoom3DPrototype"/>
    /// that fits within MaxRoomSize, chooses a rotation, shrinks the abstract room to the
    /// real room's size, and randomizes its position within the original grid cell.
    /// Uses the whitelist from the room's type-specific pack.
    /// </summary>
    private void AssignRealRooms(CEGeneratingProceduralDungeonComponent comp, CEProceduralConfig config)
    {
        var maxSize = config.MaxRoomSize;
        var step = maxSize + 1;
        var random = new Random(_random.Next());
        var maxSizeVec = new Vector2i(maxSize, maxSize);

        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            var room = comp.Rooms[i];

            // Pick the whitelist based on the room's assigned type.
            var pack = GetPackForType(config, room.RoomType);

            // Select a random room prototype that fits within MaxRoomSize.
            var roomProto = _dungeon.GetRoomPrototype(
                random,
                pack.Whitelist,
                maxSize: maxSizeVec);

            if (roomProto == null)
            {
                Log.Warning($"CEProceduralGeneratorSystem: no matching room prototype found for abstract room #{i} (type={room.RoomType}).");
                continue;
            }

            room.RoomProtoId = roomProto.ID;

            // Choose a random rotation.
            room.Rotation = _dungeon.GetRoomRotation(roomProto, random);

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
    /// Returns the <see cref="CEProceduralRoomPack"/> matching the given room type.
    /// </summary>
    private static CEProceduralRoomPack GetPackForType(CEProceduralConfig config, CEProceduralRoomType type)
    {
        return type switch
        {
            CEProceduralRoomType.Exit => config.ExitRoom,
            CEProceduralRoomType.Entrance => config.EntranceRooms,
            CEProceduralRoomType.Blessing => config.BlessingRooms,
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
            config.EntranceRooms.RoomCount.Min,
            config.EntranceRooms.RoomCount.Max + 1);
        PickFarApart(deadEnds, CEProceduralRoomType.Entrance, entranceCount);

        // Remove assigned rooms from dead-end pool.
        deadEnds.RemoveAll(r => r.RoomType != CEProceduralRoomType.General);

        // 3. Blessings: pick from remaining dead-ends, maximally far apart.
        var blessingCount = _random.Next(
            config.BlessingRooms.RoomCount.Min,
            config.BlessingRooms.RoomCount.Max + 1);
        PickFarApart(deadEnds, CEProceduralRoomType.Blessing, blessingCount);
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
