using Content.Server._CE.Procedural;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.WorldGen;
using Content.Shared._CE.WorldGen.Generators;
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
/// authored edge content (exits at canonical positions).
///
/// Rooms whose <see cref="CEDungeonRoom3DPrototype.Height"/> is 2 are treated as two-story vertical
/// connections: the same domino footprint hosts the room's lower level on floor z and its upper level on
/// floor z+1, letting mappers author stairs/ladders that join adjacent z-levels. Single (height-1) and
/// connection (height-2) rooms share one <see cref="RoomPool"/>; role is decided per floor on the fly.
/// </summary>
public sealed partial class CEHerringboneChunkGenerator : CEChunkGeneratorBase<CEHerringboneChunkGenerator>
{
    /// <summary>
    /// Room type the domino rooms are drawn from (the worldgen atlas pool). May mix height-1 single rooms
    /// and height-2 vertical-connection rooms.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CERoomTypePrototype> RoomPool;

    /// <summary>
    /// Probability in [0, 1] that any given floor of a domino column becomes a two-story vertical
    /// connection (when a height-2 room is available in <see cref="RoomPool"/>). This controls how often
    /// connections appear independently of pool weights; the specific connection room is still chosen by
    /// weight among the height-2 rooms. 0 disables connections, 1 makes every floor try to connect.
    /// </summary>
    [DataField]
    public float ConnectionChance;
}

/// <summary>
/// Executes <see cref="CEHerringboneChunkGenerator"/>. Per chunk it classifies the domino, resolves its
/// vertical role (single floor / connection bottom / connection top), picks + snapshots the room once
/// (<see cref="BeginChunk"/>), then returns per-tile content (<see cref="GenerateTile"/>).
///
/// Per-chunk scratch lives on system fields: chunk loading is synchronous and single-threaded, so only one
/// chunk is ever processed at a time.
/// </summary>
public sealed partial class CEHerringboneChunkGeneratorSystem : CEChunkGeneratorSystem<CEHerringboneChunkGenerator>
{
    [Dependency] private CEDungeonSystem _dungeon = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    // Decorrelates the per-floor connection-chance roll from room selection (which shares the same seed).
    private const int ConnectionGateSalt = unchecked((int)0x9E3779B9);

    // Per-chunk scratch (single-threaded load).
    private bool _hasRoom;
    private CERoomSnapshot? _snapshot;
    private Vector2i _halfOffset;
    private int _roomLevel;

    // Pools whose rooms have already been validated (ERROR-logged once for bad size/height). Reset on
    // prototype reload.
    private readonly HashSet<string> _validatedPools = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _validatedPools.Clear());
    }

    protected override void BeginChunk(CEHerringboneChunkGenerator gen, CEChunkGenArgs args)
    {
        _hasRoom = false;
        _snapshot = null;

        ValidatePool(gen);

        var cc = args.ChunkCoord;
        var (domino, half) = CEHerringboneLattice.Classify(cc.X, cc.Y, cc.Z);

        // Lowest chunk-Z that actually exists in this world's z-network: the connection resolver never
        // scans below it, so phantom floors beneath the world can't influence the column tiling.
        var minChunkZ = GetMinChunkZ(args);

        // Resolve this floor's vertical role within the domino column. The partition is z-invariant, so a
        // connection rooted on floor z occupies the same domino footprint on z and z+1.
        (CEDungeonRoom3DPrototype Room, int Rot)? pick;

        if (IsConnectionBottom(gen, domino, cc.Z, args.Seed, minChunkZ))
        {
            // Bottom half of a two-story connection: render room level 0.
            pick = PickConnectionRoom(gen, domino, cc.Z, args.Seed);
            _roomLevel = 0;
        }
        else if (IsConnectionBottom(gen, domino, cc.Z - 1, args.Seed, minChunkZ))
        {
            // Top half of the connection rooted on the floor below: render room level 1.
            pick = PickConnectionRoom(gen, domino, cc.Z - 1, args.Seed);
            _roomLevel = 1;
        }
        else
        {
            // Plain single floor: always a height-1 room.
            pick = PickSingleRoom(gen, domino, cc.Z, args.Seed);
            _roomLevel = 0;
        }

        if (pick is not { } chosen)
        {
            Log.Warning($"CEHerringbone: no usable room in pool '{gen.RoomPool}' for chunk {cc}.");
            return;
        }

        _snapshot = _dungeon.ReadRoomRegion(chosen.Room, chosen.Rot);
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
        result = _snapshot!.At(_roomLevel, _halfOffset.X + lt.X, _halfOffset.Y + lt.Y);
    }

    /// <summary>
    /// Lowest chunk-Z present in this world's z-network (the network's <c>SortedMin</c> depth converted to
    /// chunk-Z). Falls back to the current chunk's own floor when no network is found (off-world chunk), in
    /// which case no connection forms below it.
    /// </summary>
    private int GetMinChunkZ(CEChunkGenArgs args)
    {
        if (args.LevelGrids.Count > 0
            && _zLevels.TryGetDepthBounds(args.LevelGrids[0].Owner, out var minDepth, out _))
        {
            return FloorDiv(minDepth, CEWorldGenSystem.ChunkHeight);
        }

        return args.ChunkCoord.Z;
    }

    /// <summary>
    /// Deterministically picks a single-floor (height-1) room + rotation for a domino on a given floor.
    /// Pure function of (domino, floor, seed), so both halves of the domino agree.
    /// </summary>
    private (CEDungeonRoom3DPrototype Room, int Rot)? PickSingleRoom(
        CEHerringboneChunkGenerator gen, CEHerringboneDomino domino, int floor, int seed)
        => PickRoom(gen, domino, floor, seed, minHeight: 1, maxHeight: 1);

    /// <summary>
    /// Deterministically picks a two-story (height-2) connection room + rotation for a domino on a given
    /// floor, or null if the pool has none. Both floors of a connection call this with the bottom floor, so
    /// they agree on the same room and rotation.
    /// </summary>
    private (CEDungeonRoom3DPrototype Room, int Rot)? PickConnectionRoom(
        CEHerringboneChunkGenerator gen, CEHerringboneDomino domino, int floor, int seed)
        => PickRoom(gen, domino, floor, seed, minHeight: 2, maxHeight: 2);

    /// <summary>
    /// Deterministically picks a room of the given height range + rotation. Pure function of
    /// (domino, floor, seed, height range). Only valid-size rooms are eligible.
    /// </summary>
    private (CEDungeonRoom3DPrototype Room, int Rot)? PickRoom(
        CEHerringboneChunkGenerator gen, CEHerringboneDomino domino, int floor, int seed, int minHeight, int maxHeight)
    {
        var rng = new Random(CEHerringboneLattice.RoomSeed(domino, floor, seed));
        var expected = new Vector2i(CEWorldGenSystem.ChunkSize * 2, CEWorldGenSystem.ChunkSize);

        var room = _dungeon.GetRoomPrototype(
            rng,
            minSize: expected,
            maxSize: expected,
            roomType: gen.RoomPool,
            minHeight: minHeight,
            maxHeight: maxHeight);

        if (room == null)
            return null;

        // Horizontal domino -> 0/180, vertical -> 90/270; the extra 180 adds free variety.
        var baseRot = domino.Orientation == CEHerringboneOrientation.Horizontal ? 0 : 1;
        var rot = baseRot + 2 * rng.Next(2);
        return (room, rot);
    }

    /// <summary>
    /// Whether this floor wants to host a two-story connection. Gated by <see cref="ConnectionChance"/>
    /// (a deterministic per-floor roll, decorrelated from room selection) and by the pool actually
    /// containing a height-2 room. Pure function of (domino, floor, seed).
    /// </summary>
    private bool WantsConnection(CEHerringboneChunkGenerator gen, CEHerringboneDomino domino, int floor, int seed)
    {
        if (gen.ConnectionChance <= 0f)
            return false;

        if (gen.ConnectionChance < 1f)
        {
            var gateRng = new Random(CEHerringboneLattice.RoomSeed(domino, floor, seed) ^ ConnectionGateSalt);
            if (gateRng.NextDouble() >= gen.ConnectionChance)
                return false;
        }

        return PickConnectionRoom(gen, domino, floor, seed) is not null;
    }

    /// <summary>
    /// Whether <paramref name="floor"/> is the bottom of a two-story connection in its domino column.
    ///
    /// Connections tile a contiguous run of "wanting" (height-2) floors greedily from the bottom: within a
    /// run starting at <c>runStart</c>, bottoms sit at even offsets (runStart, runStart+2, ...) and the
    /// floors between are their consumed tops. The scan stops at <paramref name="minChunkZ"/> (the world's
    /// bottom floor). This is a pure function of <paramref name="floor"/> (the scan always starts there),
    /// so the bottom chunk and the top chunk one floor up reach the same conclusion. Any adjacent pair may
    /// connect, and several non-overlapping connections may stack in one column.
    /// </summary>
    private bool IsConnectionBottom(
        CEHerringboneChunkGenerator gen, CEHerringboneDomino domino, int floor, int seed, int minChunkZ)
    {
        if (floor < minChunkZ || !WantsConnection(gen, domino, floor, seed))
            return false;

        // Walk down to the start of the contiguous run of wanting floors that contains `floor`, bounded by
        // the world's bottom floor.
        var runStart = floor;
        while (runStart > minChunkZ && WantsConnection(gen, domino, runStart - 1, seed))
            runStart--;

        // Bottoms sit at even offsets from the run start.
        return (floor - runStart) % 2 == 0;
    }

    /// <summary>
    /// One-time-per-pool validation: ERROR-logs any room in the pool with the wrong footprint size or an
    /// unsupported height (must be 1 or 2). Such rooms are also excluded from selection, so misconfigured
    /// rooms are surfaced to mappers without breaking generation.
    /// </summary>
    private void ValidatePool(CEHerringboneChunkGenerator gen)
    {
        if (!_validatedPools.Add(gen.RoomPool))
            return;

        var expected = new Vector2i(CEWorldGenSystem.ChunkSize * 2, CEWorldGenSystem.ChunkSize);

        foreach (var proto in _proto.EnumeratePrototypes<CEDungeonRoom3DPrototype>())
        {
            if (proto.RoomType != gen.RoomPool)
                continue;

            if (proto.Size != expected)
            {
                Log.Error(
                    $"CEHerringbone: room '{proto.ID}' in pool '{gen.RoomPool}' has size {proto.Size}, expected {expected}; it will be excluded.");
            }

            if (proto.Height is < 1 or > 2)
            {
                Log.Error(
                    $"CEHerringbone: room '{proto.ID}' in pool '{gen.RoomPool}' has height {proto.Height}, expected 1 or 2; it will be excluded.");
            }
        }
    }

    /// <summary>
    /// Floored integer division (rounds toward negative infinity), correct for negative depths.
    /// </summary>
    private static int FloorDiv(int a, int b)
    {
        var q = a / b;
        if (a % b != 0 && (a < 0) != (b < 0))
            q--;
        return q;
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
