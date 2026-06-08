using System.Threading;
using Content.Server._CE.Procedural.Generators.Procedural.GenerationSteps;
using Content.Server._CE.ZLevels.Core;
using Content.Shared.Maps;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;
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
    /// The ordered list of abstract generation steps.
    /// Executed sequentially to build the full room graph before any real rooms are placed.
    /// </summary>
    [DataField]
    public List<CEDungeonGenerationStep> GenerationPlan = new();

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
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private CEDungeonSystem _dungeon = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!;

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
