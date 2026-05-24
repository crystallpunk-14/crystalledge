using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Generators.Procedural.GenerationSteps;

/// <summary>
/// Marks all rooms matching <see cref="OfType"/> that have exactly one connection as
/// <see cref="RoomType"/> (dead-ends). Rooms of other types are left unchanged.
/// </summary>
[DataDefinition]
public sealed partial class MarkDeadEnds : CEDungeonGenerationStep
{
    /// <summary>
    /// Only rooms currently of this type are considered as dead-end candidates.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CERoomTypePrototype> OfType;

    /// <summary>
    /// Room type to assign to matched dead-end rooms.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CERoomTypePrototype> RoomType;

    /// <inheritdoc/>
    public override Task Execute(CEGenerationStepContext ctx)
    {
        var comp = ctx.Comp;

        // Count connections per room index.
        var connectionCount = new Dictionary<int, int>(comp.Rooms.Count);
        foreach (var conn in comp.Connections)
        {
            connectionCount[conn.RoomA] = connectionCount.GetValueOrDefault(conn.RoomA) + 1;
            connectionCount[conn.RoomB] = connectionCount.GetValueOrDefault(conn.RoomB) + 1;
        }

        foreach (var room in comp.Rooms)
        {
            if (room.RoomType != OfType)
                continue;

            if (connectionCount.GetValueOrDefault(room.Index) == 1)
                room.RoomType = RoomType;
        }

        return Task.CompletedTask;
    }
}
