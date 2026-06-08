using Content.Shared._CE.Maths;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Procedural;

/// <summary>
/// Attached to the primary map entity while 3D procedural dungeon generation is in progress.
/// Stores the abstract 3D room graph: rooms at logical (X, Y, Z) grid coordinates
/// where Z is the z-level index.
///
/// Used by server for storing generation process, and used by client for minimap and debug overlays
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEGeneratingProceduralDungeon3DComponent : Component
{
    /// <summary>
    /// All abstract rooms placed so far.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<CEProceduralAbstractRoom3D> Rooms = new();

    /// <summary>
    /// Connections (edges) between rooms, stored as pairs of room indices.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<CEProceduralRoomConnection3D> Connections = new();
}

/// <summary>
/// An abstract room in the 3D dungeon graph.
/// <see cref="GridCoord"/> X/Y are the horizontal grid cell; Z is the z-level index.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CEProceduralAbstractRoom3D
{
    /// <summary>
    /// Logical 3D grid coordinate: X/Y = horizontal cell, Z = z-level index.
    /// </summary>
    [DataField]
    public Vector3i GridCoord;

    /// <summary>
    /// World-tile XY origin (bottom-left corner) of the room on its z-level map.
    /// Computed from GridCoord.XY * (MaxRoomSize + 1).
    /// </summary>
    [DataField]
    public Vector2i Position;

    /// <summary>
    /// XY size of the room in tiles. Defaults to (MaxRoomSize, MaxRoomSize).
    /// </summary>
    [DataField]
    public Vector2i Size;

    /// <summary>
    /// Vertical size of the room in tiles. Defaults to MaxRoomHeight.
    /// </summary>
    [DataField]
    public int Height;

    /// <summary>
    /// Prototype ID of the <see cref="CERoomTypePrototype"/> assigned to this room.
    /// </summary>
    [DataField]
    public ProtoId<CERoomTypePrototype>? RoomType;
}

/// <summary>
/// A connection (edge) between two abstract rooms in the 3D dungeon graph.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CEProceduralRoomConnection3D
{
    /// <summary>
    /// Index of the first room in <see cref="CEGeneratingProceduralDungeon3DComponent.Rooms"/>.
    /// </summary>
    [DataField]
    public int RoomA;

    /// <summary>
    /// Index of the second room in <see cref="CEGeneratingProceduralDungeon3DComponent.Rooms"/>.
    /// </summary>
    [DataField]
    public int RoomB;
}
