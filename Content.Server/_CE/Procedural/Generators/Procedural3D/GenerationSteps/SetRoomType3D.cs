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
public sealed partial class SetRoomType3D : CEDungeonGenerationStep3D
{
    /// <summary>
    /// Logical 3D grid coordinate: X/Y = horizontal cell, Z = z-level index.
    /// </summary>
    [DataField]
    public Vector3i Position;

    /// <summary>
    /// Room type to assign.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CERoomTypePrototype> RoomType;

    public override Task Execute(
        CEGeneratingProceduralDungeonComponent3D comp,
        IRobustRandom random,
        ISawmill log)
    {
        foreach (var room in comp.Rooms)
        {
            if (room.GridCoord != Position)
                continue;

            room.RoomType = RoomType;
            return Task.CompletedTask;
        }

        // No room at this position — create one.
        var newRoom = new CEProceduralAbstractRoom3D
        {
            Index = comp.Rooms.Count,
            GridCoord = Position,
            RoomType = RoomType,
        };
        comp.Rooms.Add(newRoom);
        return Task.CompletedTask;
    }
}
