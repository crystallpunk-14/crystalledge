using Content.Shared.Destructible.Thresholds;
using Content.Shared.Whitelist;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
///
/// </summary>
public sealed partial class CEProceduralConfig : CEDungeonGeneratorConfigBase<CEProceduralConfig>
{
    /// <summary>
    /// Although the generator works with z-levels, only one of these z-levels is “playable,”
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
    public List<CEProceduralRoomPack> Specials = new();
}

[DataDefinition]
public sealed partial class CEProceduralRoomPack
{
    /// <summary>
    /// Filtering rooms that are suitable for this pack
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// the number of rooms generated in this pack
    /// </summary>
    [DataField]
    public MinMax RoomCount = new(1, 1);
}

public enum CEProceduralRoomGenerationType
{
    /// <summary>
    /// ??
    /// </summary>
    DeadEnd,
}

/// <summary>
///
/// </summary>
public sealed partial class CEProceduralGeneratorSystem : CEDungeonGeneratorSystem<CEProceduralConfig>
{
    protected override void Generate(ref CEDungeonGenerateEvent<CEProceduralConfig> args)
    {

    }
}
