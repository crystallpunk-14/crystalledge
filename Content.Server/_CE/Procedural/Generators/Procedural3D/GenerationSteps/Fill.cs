using System.Threading.Tasks;
using Content.Shared._CE.Maths;
using Content.Shared._CE.Procedural;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural3D.GenerationSteps;

/// <summary>
/// Fills an axis-aligned box of grid cells with rooms of the given type.
/// <see cref="From"/> and <see cref="To"/> are the inclusive bounds; order does not matter.
/// </summary>
[DataDefinition]
public sealed partial class Fill : CEDungeonGenerationStep3D
{
    /// <summary>One corner of the filled box (inclusive).</summary>
    [DataField(required: true)]
    public Vector3i From;

    /// <summary>The opposite corner of the filled box (inclusive).</summary>
    [DataField(required: true)]
    public Vector3i To;

    /// <summary>
    /// Room type assigned to every room in the region.
    /// Null leaves the type unchanged for existing rooms and unset for new ones.
    /// </summary>
    [DataField("proto", required: true)] //user should place roomType: null if they wanna clear area. Intended required tag
    public ProtoId<CERoomTypePrototype>? RoomType;

    /// <summary>
    /// When true (default), connects each room to its +X, +Y and +Z neighbour inside the region.
    /// </summary>
    [DataField]
    public bool AutoConnect = true;

    public override Task Execute(
        CEGeneratingProceduralDungeon3DComponent comp,
        Dictionary<Vector3i, int> roomsByCoord,
        int maxRoomSize,
        int maxRoomHeight,
        IRobustRandom random,
        ISawmill log)
    {
        var minX = Math.Min(From.X, To.X);
        var maxX = Math.Max(From.X, To.X);
        var minY = Math.Min(From.Y, To.Y);
        var maxY = Math.Max(From.Y, To.Y);
        var minZ = Math.Min(From.Z, To.Z);
        var maxZ = Math.Max(From.Z, To.Z);

        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        for (var z = minZ; z <= maxZ; z++)
            SetRoom(comp, roomsByCoord, new Vector3i(x, y, z), maxRoomSize, maxRoomHeight, RoomType);

        if (RoomType is null || !AutoConnect)
            return Task.CompletedTask;

        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        for (var z = minZ; z <= maxZ; z++)
        {
            var coord = new Vector3i(x, y, z);
            if (x < maxX) TryConnect(comp, roomsByCoord, coord, new Vector3i(x + 1, y, z), log);
            if (y < maxY) TryConnect(comp, roomsByCoord, coord, new Vector3i(x, y + 1, z), log);
            if (z < maxZ) TryConnect(comp, roomsByCoord, coord, new Vector3i(x, y, z + 1), log);
        }

        return Task.CompletedTask;
    }
}
