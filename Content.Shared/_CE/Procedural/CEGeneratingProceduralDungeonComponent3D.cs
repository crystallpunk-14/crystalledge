using Content.Shared._CE.Maths;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Procedural;

/// <summary>
/// Attached to the primary map entity while 3D procedural dungeon generation is in progress.
/// Stores the abstract 3D room graph: rooms at logical (X, Y, Z) grid coordinates
/// where Z is the z-level index.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEGeneratingProceduralDungeonComponent3D : Component
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
    /// Index of this room in the <see cref="CEGeneratingProceduralDungeonComponent3D.Rooms"/> list.
    /// </summary>
    [DataField]
    public int Index;

    /// <summary>
    /// Logical 3D grid coordinate: X/Y = horizontal cell, Z = z-level index.
    /// </summary>
    [DataField]
    public Vector3i GridCoord;

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
    [DataField]
    public int RoomA;

    [DataField]
    public int RoomB;
}
