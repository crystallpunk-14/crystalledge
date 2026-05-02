using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Generators.Procedural.GenerationSteps;

/// <summary>
/// Sets the type of the room at the given grid coordinate.
/// If no room exists at that position, a new room is created and added to the dungeon.
/// </summary>
[DataDefinition]
public sealed partial class SetRoomType : CEDungeonGenerationStep
{
    /// <summary>
    /// Logical grid coordinate of the room to set or create.
    /// </summary>
    [DataField]
    public Vector2i Position;

    /// <summary>
    /// Room type to assign.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CERoomTypePrototype> RoomType;

    /// <inheritdoc/>
    public override Task Execute(CEGenerationStepContext ctx)
    {
        foreach (var room in ctx.Comp.Rooms)
        {
            if (room.GridCoord != Position)
                continue;

            room.RoomType = RoomType;
            return Task.CompletedTask;
        }

        // No room at this position — create one.
        var gridStep = ctx.MaxRoomSize + 1;
        var roomSize = new Vector2i(ctx.MaxRoomSize, ctx.MaxRoomSize);
        var newRoom = new CEProceduralAbstractRoom
        {
            Index = ctx.Comp.Rooms.Count,
            GridCoord = Position,
            Position = new Vector2i(Position.X * gridStep, Position.Y * gridStep),
            Size = roomSize,
            RoomType = RoomType,
        };
        ctx.Comp.Rooms.Add(newRoom);
        return Task.CompletedTask;
    }
}
