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
        CEGeneratingProceduralDungeon3DComponent comp,
        int maxRoomSize,
        int maxRoomHeight,
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

        // No room at this position — create one with pre-computed world dimensions.
        var gridStep = maxRoomSize + 1;
        var newRoom = new CEProceduralAbstractRoom3D
        {
            Index = comp.Rooms.Count,
            GridCoord = Position,
            Position = new Vector2i(Position.X * gridStep, Position.Y * gridStep),
            Size = new Vector2i(maxRoomSize, maxRoomSize),
            Height = maxRoomHeight,
            RoomType = RoomType,
        };
        comp.Rooms.Add(newRoom);
        return Task.CompletedTask;
    }
}
