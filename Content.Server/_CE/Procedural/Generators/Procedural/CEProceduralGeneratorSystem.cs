using Content.Shared._CE.Procedural;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Whitelist;
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
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly CEDungeonSystem _dungeon = default!;

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

        // Assign real room prototypes, apply rotation, resize and randomize position.
        AssignRealRooms(comp, config);

        Dirty(mapUid, comp);

        // Report results.
        args.MapUid = mapUid;
        args.MapId = mapId;
        args.Handled = true;
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

            // Select a random room prototype that fits within MaxRoomSize.
            var roomProto = _dungeon.GetRoomPrototype(
                random,
                config.GeneralRooms.Whitelist,
                maxSize: maxSizeVec);

            if (roomProto == null)
            {
                Log.Warning($"CEProceduralGeneratorSystem: no matching room prototype found for abstract room #{i}.");
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
}
