using System.Threading.Tasks;
using Content.Shared._CE.Maths;
using Content.Shared._CE.Procedural;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural3D.GenerationSteps;

/// <summary>
/// Sets the type of the room at the given 3D grid coordinate.
/// If no room exists at that position, a new room is created and added to the dungeon.
/// </summary>
[DataDefinition]
public sealed partial class Set : CEDungeonGenerationStep3D
{
    /// <summary>
    /// Logical 3D grid coordinate: X/Y = horizontal cell, Z = z-level index.
    /// </summary>
    [DataField("pos")]
    public Vector3i Position;

    /// <summary>
    /// Room type to assign. Null erases the room at <see cref="Position"/> and removes all its connections.
    /// </summary>
    [DataField("proto", required: true)] //user should place roomType: null if they wanna clear area. Intended required tag
    public ProtoId<CERoomTypePrototype>? RoomType;

    public override Task Execute(
        CEGeneratingProceduralDungeon3DComponent comp,
        Dictionary<Vector3i, int> roomsByCoord,
        int maxRoomSize,
        int maxRoomHeight,
        IRobustRandom random,
        ISawmill log)
    {
        SetRoom(comp, roomsByCoord, Position, maxRoomSize, maxRoomHeight, RoomType);
        return Task.CompletedTask;
    }
}
