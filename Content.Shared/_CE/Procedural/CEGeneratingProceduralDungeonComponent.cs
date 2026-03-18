using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Procedural;

/// <summary>
/// Attached to a map entity while procedural dungeon generation is in progress.
/// Stores the abstract room graph: room positions/sizes on a logical grid and
/// connections between neighbouring rooms.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEGeneratingProceduralDungeonComponent : Component
{
    /// <summary>
    /// All abstract rooms placed so far.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<CEProceduralAbstractRoom> Rooms = new();

    /// <summary>
    /// Connections (edges) between rooms, stored as pairs of room indices.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<CEProceduralRoomConnection> Connections = new();
}

/// <summary>
/// An abstract room in the procedural dungeon graph.
/// Position is the world-tile origin (bottom-left corner).
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CEProceduralAbstractRoom
{
    /// <summary>
    /// Index of this room in the <see cref="CEGeneratingProceduralDungeonComponent.Rooms"/> list.
    /// </summary>
    [DataField]
    public int Index;

    /// <summary>
    /// World-tile position (bottom-left corner of the room).
    /// Computed from the logical grid coordinate: gridPos * (MaxRoomSize + 1).
    /// </summary>
    [DataField]
    public Vector2i Position;

    /// <summary>
    /// Size of the room in tiles.
    /// Defaults to (MaxRoomSize, MaxRoomSize) at this stage.
    /// </summary>
    [DataField]
    public Vector2i Size;

    /// <summary>
    /// Logical grid coordinate used for overlap detection.
    /// Each room occupies exactly one cell since all rooms are the same size.
    /// </summary>
    [DataField]
    public Vector2i GridCoord;

    /// <summary>
    /// The prototype ID of the real room selected for this abstract slot.
    /// Null until a real room has been assigned.
    /// </summary>
    [DataField]
    public ProtoId<CEDungeonRoom3DPrototype>? RoomProtoId;

    /// <summary>
    /// Rotation chosen for the real room (0, 90, 180 or 270 degrees).
    /// </summary>
    [DataField]
    public Angle Rotation;

    /// <summary>
    /// The type/role of this room in the dungeon.
    /// </summary>
    [DataField]
    public CEProceduralRoomType RoomType = CEProceduralRoomType.General;
}

/// <summary>
/// The functional role of an abstract room in the procedural dungeon.
/// </summary>
public enum CEProceduralRoomType : byte
{
    /// <summary>Normal room.</summary>
    General,
    /// <summary>Dungeon exit (placed at 0,0).</summary>
    Exit,
    /// <summary>Dungeon entrance (dead-end, far from others).</summary>
    Entrance,
    /// <summary>Blessing/treasure room (dead-end, far from others).</summary>
    Blessing,
    /// <summary>Dead-end room (1 connection, not assigned a special role).</summary>
    DeadEnd,
}

/// <summary>
/// A connection (edge) between two abstract rooms in the dungeon graph.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CEProceduralRoomConnection
{
    /// <summary>
    /// Index of the first room.
    /// </summary>
    [DataField]
    public int RoomA;

    /// <summary>
    /// Index of the second room.
    /// </summary>
    [DataField]
    public int RoomB;
}
