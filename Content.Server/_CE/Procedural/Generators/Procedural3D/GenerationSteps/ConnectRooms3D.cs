using System.Threading.Tasks;
using Content.Shared._CE.Maths;
using Content.Shared._CE.Procedural;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural3D.GenerationSteps;

/// <summary>
/// Adds a connection between two rooms at the specified 3D grid coordinates.
/// Both rooms must already exist in the dungeon graph.
/// </summary>
[DataDefinition]
public sealed partial class ConnectRooms3D : CEDungeonGenerationStep3D
{
    [DataField(required: true)]
    public Vector3i RoomA;

    [DataField(required: true)]
    public Vector3i RoomB;

    public override Task Execute(
        CEGeneratingProceduralDungeon3DComponent comp,
        int maxRoomSize,
        int maxRoomHeight,
        IRobustRandom random,
        ISawmill log)
    {
        int? indexA = null, indexB = null;

        foreach (var room in comp.Rooms)
        {
            if (room.GridCoord == RoomA)
                indexA = room.Index;
            else if (room.GridCoord == RoomB)
                indexB = room.Index;

            if (indexA.HasValue && indexB.HasValue)
                break;
        }

        if (indexA is null)
        {
            log.Warning($"ConnectRooms3D: no room at {RoomA}");
            return Task.CompletedTask;
        }

        if (indexB is null)
        {
            log.Warning($"ConnectRooms3D: no room at {RoomB}");
            return Task.CompletedTask;
        }

        comp.Connections.Add(new CEProceduralRoomConnection3D
        {
            RoomA = indexA.Value,
            RoomB = indexB.Value,
        });

        return Task.CompletedTask;
    }
}
