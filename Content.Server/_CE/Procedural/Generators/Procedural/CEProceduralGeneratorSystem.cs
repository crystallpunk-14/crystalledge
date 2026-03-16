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
/// then places actual rooms on the map.
/// Split into partial classes by responsibility:
/// <list type="bullet">
///   <item><c>CEProceduralGeneratorSystem.Graph.cs</c>  abstract room graph construction.</item>
///   <item><c>CEProceduralGeneratorSystem.RoomAssignment.cs</c>  room type and prototype assignment.</item>
///   <item><c>CEProceduralGeneratorSystem.Spawning.cs</c>  room spawning and wall placement.</item>
///   <item><c>CEProceduralGeneratorSystem.Corridors.cs</c>  corridor pathfinding and placement.</item>
///   <item><c>CEProceduralGeneratorSystem.Compaction.cs</c>  room compaction toward parents.</item>
/// </list>
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
}
