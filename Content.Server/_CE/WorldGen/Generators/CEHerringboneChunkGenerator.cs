using Content.Server._CE.Procedural;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.WorldGen;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.Generators;

/// <summary>
/// Fills a chunk with one half of a herringbone domino room drawn from a 3D room atlas. A domino spans two
/// chunks; each chunk is exactly half of one room. Room choice is a pure function of the domino coordinate
/// + world seed (see <see cref="CEHerringboneLattice"/>), so it is stable regardless of load order or
/// approach direction.
///
/// Rooms are placed flush against each other with no gaps and no seam-sealing — rooms connect via their own
/// authored edge content (exits at canonical positions). Vertical (up/down) connections are a later phase.
/// </summary>
public sealed partial class CEHerringboneChunkGenerator : CEChunkGeneratorBase<CEHerringboneChunkGenerator>
{
    /// <summary>
    /// Room type the domino rooms are drawn from (the worldgen atlas pool).
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CERoomTypePrototype> RoomPool;
}

/// <summary>
/// Executes <see cref="CEHerringboneChunkGenerator"/>. Per chunk it classifies the domino, picks + snapshots
/// the room half once (<see cref="BeginChunk"/>), then returns per-tile content (<see cref="GenerateTile"/>).
///
/// Per-chunk scratch lives on system fields: chunk loading is synchronous and single-threaded, so only one
/// chunk is ever processed at a time.
/// </summary>
public sealed partial class CEHerringboneChunkGeneratorSystem : CEChunkGeneratorSystem<CEHerringboneChunkGenerator>
{
    [Dependency] private CEDungeonSystem _dungeon = default!;

    // Per-chunk scratch (single-threaded load).
    private bool _hasRoom;
    private CERoomSnapshot? _snapshot;
    private Vector2i _halfOffset;

    protected override void BeginChunk(CEHerringboneChunkGenerator gen, CEChunkGenArgs args)
    {
        _hasRoom = false;
        _snapshot = null;

        var cc = args.ChunkCoord;
        var (domino, half) = CEHerringboneLattice.Classify(cc.X, cc.Y, cc.Z);

        // Room choice depends only on the domino + cz + seed, so both halves at the same z-level agree.
        var rng = new Random(CEHerringboneLattice.RoomSeed(domino, cc.Z, args.Seed));

        var room = _dungeon.GetRoomPrototype(rng, roomType: gen.RoomPool);
        if (room == null)
        {
            Log.Warning($"CEHerringbone: no room in pool '{gen.RoomPool}' for chunk {cc}.");
            return;
        }

        var expected = new Vector2i(CEWorldGenSystem.ChunkSize * 2, CEWorldGenSystem.ChunkSize);
        if (room.Size != expected || room.Height != CEWorldGenSystem.ChunkHeight)
        {
            Log.Warning(
                $"CEHerringbone: room '{room.ID}' is {room.Size} x{room.Height}, expected {expected} x{CEWorldGenSystem.ChunkHeight}.");
            return;
        }

        // Horizontal domino -> 0/180, vertical -> 90/270; the extra 180 adds free variety.
        var baseRot = domino.Orientation == CEHerringboneOrientation.Horizontal ? 0 : 1;
        var rot = baseRot + 2 * rng.Next(2);

        _snapshot = _dungeon.ReadRoomRegion(room, rot);
        if (_snapshot == null)
            return;

        // half 0 == anchor cell (left/bottom), half 1 == the other.
        _halfOffset = domino.Orientation == CEHerringboneOrientation.Horizontal
            ? new Vector2i(half * CEWorldGenSystem.ChunkSize, 0)
            : new Vector2i(0, half * CEWorldGenSystem.ChunkSize);

        _hasRoom = true;
    }

    protected override void GenerateTile(CEHerringboneChunkGenerator gen, in CETileGenContext ctx, ref CETileContent result)
    {
        if (!_hasRoom)
            return;

        var lt = ctx.LocalTile;
        result = _snapshot!.At(ctx.Level, _halfOffset.X + lt.X, _halfOffset.Y + lt.Y);
    }
}

/// <summary>
/// A read-only, already-rotated snapshot of an atlas room: per z-level, the content of every cell of the
/// room's (rotated) footprint. Produced once per chunk load and indexed O(1) per tile. Entities/decals carry
/// their rotation so directional content survives the snapshot. Used only by the herringbone generator.
/// </summary>
public sealed class CERoomSnapshot
{
    /// <summary>
    /// Footprint size in tiles, after rotation.
    /// </summary>
    public readonly Vector2i Size;

    /// <summary>
    /// Number of z-levels (== room height).
    /// </summary>
    public readonly int Height;

    /// <summary>
    /// Cell content, indexed <c>[level][x + y * Size.X]</c>.
    /// </summary>
    public readonly CETileContent[][] Cells;

    public CERoomSnapshot(Vector2i size, int height)
    {
        Size = size;
        Height = height;
        Cells = new CETileContent[height][];
        for (var i = 0; i < height; i++)
            Cells[i] = new CETileContent[size.X * size.Y];
    }

    /// <summary>
    /// Content at the given level and footprint coordinate, or an empty (void) cell if out of range.
    /// </summary>
    public CETileContent At(int level, int x, int y)
    {
        if (level < 0 || level >= Height || x < 0 || x >= Size.X || y < 0 || y >= Size.Y)
            return default;

        return Cells[level][x + y * Size.X];
    }
}
