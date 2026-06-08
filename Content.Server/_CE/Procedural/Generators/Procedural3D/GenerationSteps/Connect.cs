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
public sealed partial class Connect : CEDungeonGenerationStep3D
{
    [DataField(required: true)]
    public Vector3i Pos1;

    [DataField(required: true)]
    public Vector3i Pos2;

    public override Task Execute(
        CEGeneratingProceduralDungeon3DComponent comp,
        Dictionary<Vector3i, int> roomsByCoord,
        int maxRoomSize,
        int maxRoomHeight,
        IRobustRandom random,
        ISawmill log)
    {
        TryConnect(comp, roomsByCoord, Pos1, Pos2, log);
        return Task.CompletedTask;
    }
}
