using System.Threading;
using Content.Server._CE.Procedural.Prototypes;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Maps;
using Robust.Shared.CPUJob.JobQueues;
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

    /// <summary>
    /// Room type prototype for general rooms.
    /// </summary>
    [DataField]
    public ProtoId<CERoomTypePrototype>? GeneralRooms;

    [DataField]
    public MinMax GeneralCount = new(30, 50);

    /// <summary>
    /// Room type prototype for the exit room (placed at grid origin).
    /// </summary>
    [DataField]
    public ProtoId<CERoomTypePrototype>? ExitRoom;

    /// <summary>
    /// Room type prototype for entrance rooms (dead-ends, maximally far apart).
    /// </summary>
    [DataField]
    public ProtoId<CERoomTypePrototype>? EntranceRooms;

    [DataField]
    public MinMax EntranceCount = new(2, 2);

    /// <summary>
    /// Room type prototype for blessing rooms (dead-ends, maximally far apart).
    /// </summary>
    [DataField]
    public ProtoId<CERoomTypePrototype>? BlessingRooms;

    [DataField]
    public MinMax BlessingCount = new(2, 2);

    /// <summary>
    /// Room type prototype for treasure rooms (dead-ends, generated after blessing rooms).
    /// </summary>
    [DataField]
    public ProtoId<CERoomTypePrototype>? TreasureRooms;

    [DataField]
    public MinMax TreasureCount = new(0, 0);

    /// <summary>
    /// Room type prototype for dead-end rooms (remaining dead-ends after special types).
    /// </summary>
    [DataField]
    public ProtoId<CERoomTypePrototype>? DeadEndRooms;

    /// <summary>
    /// Shared components applied to every z-level map in the dungeon's z-network
    /// (e.g. MapAtmosphere, MapLight, CEZLevelMapRoof).
    /// </summary>
    [DataField]
    public ComponentRegistry Components = new();

    /// <summary>
    /// Number of extra cyclic connections to add between adjacent General rooms.
    /// These connections create loops in the dungeon graph, providing alternative routes.
    /// Pairs of rooms are sorted by distance from center (farthest first).
    /// </summary>
    [DataField]
    public MinMax CycleCount = new(0, 0);

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

    /// <summary>
    /// Entity prototype spawned as a door at corridor endpoints (between a room and its corridor).
    /// Rotated so its front faces the room.
    /// </summary>
    [DataField]
    public EntProtoId DoorPrototype = "CEWoodenDoor";
}

/// <summary>
/// Procedural dungeon generator. Builds an abstract room graph on a logical grid
/// then places actual rooms on the map.
/// <para>
/// Generation runs asynchronously via <see cref="CEProceduralDungeonJob"/>,
/// which yields cooperatively across frames using <see cref="Job{T}.SuspendIfOutOfTime"/>.
/// </para>
/// Split into partial classes by responsibility:
/// <list type="bullet">
///   <item><c>CEProceduralGeneratorSystem.Graph.cs</c>  abstract room graph construction.</item>
///   <item><c>CEProceduralGeneratorSystem.RoomAssignment.cs</c>  room type and prototype assignment.</item>
///   <item><c>CEProceduralGeneratorSystem.Spawning.cs</c>  room spawning and wall placement.</item>
///   <item><c>CEProceduralGeneratorSystem.Corridors.cs</c>  corridor pathfinding and placement.</item>
///   <item><c>CEProceduralGeneratorSystem.Cycles.cs</c>  cyclic route injection.</item>
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
    /// Cardinal directions on the logical grid: right, left, up, down.
    /// </summary>
    internal static readonly Vector2i[] Directions =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    protected override Job<CEDungeonGenerateResult> CreateJob(
        CEProceduralConfig config,
        double maxTime,
        CancellationToken cancellation)
    {
        return new CEProceduralDungeonJob(
            Log,
            maxTime,
            EntityManager,
            _proto,
            _random,
            _maps,
            _zLevels,
            this,
            config,
            cancellation);
    }
}
