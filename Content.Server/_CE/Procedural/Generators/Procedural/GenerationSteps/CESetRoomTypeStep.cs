using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Generators.Procedural.GenerationSteps;

/// <summary>
/// Changes the type of the room at the given grid coordinate.
/// Has no effect if no room exists at that position.
/// </summary>
[DataDefinition]
public sealed partial class CESetRoomTypeStep : CEDungeonGenerationStep
{
    /// <summary>
    /// Logical grid coordinate of the room to retype.
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

        ctx.Log.Warning($"SetRoomTypeStep found no room at grid coord {Position}.");
        return Task.CompletedTask;
    }
}
