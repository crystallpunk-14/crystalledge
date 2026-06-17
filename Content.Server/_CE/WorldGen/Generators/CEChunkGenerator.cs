using System.Numerics;
using Content.Server._CE.WorldGen.PostProcess;
using Content.Server.Decals;
using Content.Shared._CE.Maths;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.Generators;

/// <summary>
/// Data-only base class for chunk generators.
/// Logic is handled by systems subscribing to <see cref="CEChunkGenEvent{T}"/>,
/// mirroring the CE entity-effect pattern (data + handling system in one file).
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEChunkGenerator
{
    /// <summary>
    /// Dispatches this generator by raising a typed broadcast event.
    /// </summary>
    public abstract void Generate(CEChunkGenArgs args);
}

/// <summary>
/// Generic base that provides automatic event dispatch for concrete generator types.
/// Concrete generators inherit from this instead of <see cref="CEChunkGenerator"/> directly.
/// </summary>
public abstract partial class CEChunkGeneratorBase<T> : CEChunkGenerator where T : CEChunkGeneratorBase<T>
{
    public override void Generate(CEChunkGenArgs args)
    {
        if (this is not T typed)
            return;

        var ev = new CEChunkGenEvent<T>(typed, args);
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }
}

/// <summary>
/// What a generator wants placed on a single tile: at most one tile, one entity, one decal.
/// A null <see cref="Tile"/> means the cell stays void — entity and decal are then ignored.
/// </summary>
public struct CETileContent
{
    /// <summary>
    /// Tile to place (null == leave the cell void; entity and decal are ignored).
    /// </summary>
    public ProtoId<ContentTileDefinition>? Tile;

    /// <summary>
    /// Entity to spawn on this tile.
    /// </summary>
    public CEEntitySpec? Entity;

    /// <summary>
    /// Decal to place on this tile.
    /// </summary>
    public CEDecalSpec? Decal;

    /// <summary>
    /// Resets all fields to empty for reuse across tiles.
    /// </summary>
    public void Clear()
    {
        Tile = null;
        Entity = null;
        Decal = null;
    }
}

/// <summary>
/// One entity to spawn on a tile, carrying its rotation and whether it should be anchored.
/// </summary>
public readonly record struct CEEntitySpec(EntProtoId Proto, Angle Rotation, bool Anchored = true);

/// <summary>
/// One decal to place on a tile. <paramref name="Offset"/> is the sub-tile position within the
/// tile (grid units, already including any rotation nudge), <paramref name="Rotation"/> the final
/// decal angle.
/// </summary>
public readonly record struct CEDecalSpec(
    ProtoId<DecalPrototype> Id,
    Vector2 Offset,
    Angle Rotation,
    Color? Color = null,
    bool Cleanable = false);

/// <summary>
/// Per-tile input handed to a generator. Deliberately carries NO write access to the grid — a
/// generator can only describe content for the asked tile, never write outside its chunk. The base
/// system owns the loop, bounds, batching and load/unload bookkeeping. The <see cref="Grid"/> field
/// is provided for read-only per-tile queries (e.g. by post-process biome layers) but must not be
/// used to write tiles.
/// </summary>
public readonly struct CETileGenContext
{
    /// <summary>
    /// Entity manager for read-only lookups.
    /// </summary>
    public readonly IEntityManager EntityManager;

    /// <summary>
    /// Chunk this tile belongs to (x, y, chunk-z).
    /// </summary>
    public readonly Vector3i ChunkCoord;

    /// <summary>
    /// Tile position inside the chunk: 0..<see cref="CEWorldGenSystem.ChunkSize"/>-1.
    /// </summary>
    public readonly Vector2i LocalTile;

    /// <summary>
    /// Z-level offset within the chunk: 0..<see cref="CEWorldGenSystem.ChunkHeight"/>-1.
    /// </summary>
    public readonly int Level;

    /// <summary>
    /// Absolute tile index on the level grid.
    /// </summary>
    public readonly Vector2i WorldTile;

    /// <summary>
    /// Absolute world depth of this level.
    /// </summary>
    public readonly int Depth;

    /// <summary>
    /// World seed.
    /// </summary>
    public readonly int Seed;

    /// <summary>
    /// The level grid this tile lives on. Passed to post-process layers (e.g. biome system) for
    /// read-only per-tile queries; generators themselves must not write to the grid directly.
    /// </summary>
    public readonly Entity<MapGridComponent> Grid;

    public CETileGenContext(
        IEntityManager entityManager,
        Vector3i chunkCoord,
        Vector2i localTile,
        int level,
        Vector2i worldTile,
        int depth,
        int seed,
        Entity<MapGridComponent> grid)
    {
        EntityManager = entityManager;
        ChunkCoord = chunkCoord;
        LocalTile = localTile;
        Level = level;
        WorldTile = worldTile;
        Depth = depth;
        Seed = seed;
        Grid = grid;
    }
}

/// <summary>
/// Everything a chunk generator needs to fill a whole chunk — all of its z-levels.
/// A chunk spans <see cref="LevelGrids"/>.Count z-levels; the generator is responsible
/// for filling every level. The reference-typed output collections are populated by the
/// base system and read back by the world-gen system for load/unload bookkeeping.
/// </summary>
public sealed class CEChunkGenArgs(
    IEntityManager entityManager,
    IReadOnlyList<Entity<MapGridComponent>> levelGrids,
    Vector3i chunkCoord,
    Vector2i chunkOriginTile,
    int seed,
    IReadOnlyList<HashSet<Vector2i>> modifiedTilesPerLevel,
    List<(int Level, Vector2i Tile, Tile Value)> generatedTiles,
    List<(int Level, EntityUid Ent, Vector2i Tile)> spawnedEntities,
    IReadOnlyList<CEWorldPostProcessLayer> postProcess)
{
    /// <summary>
    /// Entity manager for the generator dispatch.
    /// </summary>
    public readonly IEntityManager EntityManager = entityManager;

    /// <summary>
    /// One grid per z-level of the chunk; index == depth offset (0 == bottom).
    /// </summary>
    public readonly IReadOnlyList<Entity<MapGridComponent>> LevelGrids = levelGrids;

    /// <summary>
    /// Chunk coordinate (x, y, chunk-z).
    /// </summary>
    public readonly Vector3i ChunkCoord = chunkCoord;

    /// <summary>
    /// Bottom-left tile of the chunk on the level grid.
    /// </summary>
    public readonly Vector2i ChunkOriginTile = chunkOriginTile;

    /// <summary>
    /// World seed.
    /// </summary>
    public readonly int Seed = seed;

    /// <summary>
    /// Tiles already edited by players, per level — never overwrite these.
    /// </summary>
    public readonly IReadOnlyList<HashSet<Vector2i>> ModifiedTilesPerLevel = modifiedTilesPerLevel;

    /// <summary>
    /// Output: every tile the generator placed, tagged with its level, for unload diffing.
    /// </summary>
    public readonly List<(int Level, Vector2i Tile, Tile Value)> GeneratedTiles = generatedTiles;

    /// <summary>
    /// Output: every entity the generator spawned, tagged with its level, for unload cleanup.
    /// </summary>
    public readonly List<(int Level, EntityUid Ent, Vector2i Tile)> SpawnedEntities = spawnedEntities;

    /// <summary>
    /// Ordered post-process layers applied per-tile after <see cref="CEChunkGeneratorSystem{T}.GenerateTile"/>.
    /// </summary>
    public readonly IReadOnlyList<CEWorldPostProcessLayer> PostProcess = postProcess;
}

/// <summary>
/// Broadcast event raised when a chunk generator is dispatched.
/// </summary>
[ByRefEvent]
public record struct CEChunkGenEvent<T>(T Generator, CEChunkGenArgs Args) where T : CEChunkGeneratorBase<T>;

/// <summary>
/// Abstract base system for handling chunk generators. Owns the per-tile loop: for every tile of every
/// z-level it asks the concrete generator (via <see cref="GenerateTile"/>) what to place, then writes
/// tiles (batched per level), spawns + anchors entities and adds decals, recording everything into the
/// args output lists for unload bookkeeping.
///
/// The generator never touches the grid, so it is structurally impossible to write outside the chunk.
///
/// Per-chunk scratch may be stored in <see cref="BeginChunk"/> on system fields: chunk loading is
/// synchronous and single-threaded (see <see cref="CEWorldGenSystem"/>), so only one chunk is ever in
/// flight at a time.
/// </summary>
public abstract partial class CEChunkGeneratorSystem<T> : EntitySystem where T : CEChunkGeneratorBase<T>
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private TileSystem _tile = default!;

    // Per-level scratch (single-threaded load, see class doc).
    private readonly List<(Vector2i, Tile)> _tileBatch = new();
    private readonly List<(Vector2i Tile, CEEntitySpec Spec)> _pendingEntities = new();
    private readonly List<(Vector2i Tile, CEDecalSpec Spec)> _pendingDecals = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEChunkGenEvent<T>>(OnGenerate);
    }

    private void OnGenerate(ref CEChunkGenEvent<T> ev)
    {
        var gen = ev.Generator;
        var a = ev.Args;

        BeginChunk(gen, a);

        var content = new CETileContent();
        var hasPostProcess = a.PostProcess.Count > 0;

        for (var level = 0; level < a.LevelGrids.Count; level++)
        {
            var grid = a.LevelGrids[level];
            var modified = a.ModifiedTilesPerLevel[level];
            var depth = a.ChunkCoord.Z * CEWorldGenSystem.ChunkHeight + level;

            _tileBatch.Clear();
            _pendingEntities.Clear();
            _pendingDecals.Clear();

            for (var x = 0; x < CEWorldGenSystem.ChunkSize; x++)
            {
                for (var y = 0; y < CEWorldGenSystem.ChunkSize; y++)
                {
                    var local = new Vector2i(x, y);
                    var world = new Vector2i(a.ChunkOriginTile.X + x, a.ChunkOriginTile.Y + y);

                    if (modified.Contains(world))
                        continue;

                    content.Clear();
                    var ctx = new CETileGenContext(EntityManager, a.ChunkCoord, local, level, world, depth, a.Seed, grid);
                    GenerateTile(gen, ctx, ref content);

                    // Post-process layers run after generation, before the void-tile gate.
                    // Only entered when layers are configured AND the tile is non-void.
                    if (hasPostProcess && content.Tile != null)
                    {
                        for (var i = 0; i < a.PostProcess.Count; i++)
                        {
                            var layerSeed = HashCode.Combine(a.Seed, world.X, world.Y, level, i);
                            a.PostProcess[i].Process(EntityManager, ctx, ref content, layerSeed);
                            // A layer may void the tile — stop processing remaining layers.
                            if (content.Tile == null)
                                break;
                        }
                    }

                    // A void tile carries nothing — skip its entity/decal too.
                    if (content.Tile is not { } tileProto)
                        continue;

                    var tileDef = (ContentTileDefinition) _tileDef[tileProto];
                    var variantSeed = HashCode.Combine(a.Seed, world.X, world.Y, level);
                    var tile = _tile.GetVariantTile(tileDef, variantSeed);
                    _tileBatch.Add((world, tile));
                    a.GeneratedTiles.Add((level, world, tile));

                    if (content.Entity is { } entSpec)
                        _pendingEntities.Add((world, entSpec));

                    if (content.Decal is { } decalSpec)
                        _pendingDecals.Add((world, decalSpec));
                }
            }

            // Tiles first so anchoring/decals land on real tiles.
            _map.SetTiles(grid.Owner, grid.Comp, _tileBatch);

            foreach (var (tile, spec) in _pendingEntities)
            {
                var coords = _map.GridTileToLocal(grid.Owner, grid.Comp, tile);
                var ent = Spawn(spec.Proto, coords);
                var xform = Transform(ent);

                _transform.SetLocalRotation(ent, spec.Rotation, xform);

                if (spec.Anchored && !xform.Anchored)
                    _transform.AnchorEntity((ent, xform), grid, tile);

                a.SpawnedEntities.Add((level, ent, tile));
            }

            foreach (var (tile, spec) in _pendingDecals)
            {
                var coords = new EntityCoordinates(grid.Owner, tile + spec.Offset);
                _decals.TryAddDecal(spec.Id, coords, out _, spec.Color, spec.Rotation, cleanable: spec.Cleanable);
            }
        }

        EndChunk(gen);
    }

    /// <summary>
    /// Optional per-chunk precompute (e.g. pick a room and snapshot it).
    /// </summary>
    protected virtual void BeginChunk(T gen, CEChunkGenArgs args)
    {
    }

    /// <summary>
    /// Per-tile content. Called once for every non-player-modified tile of every level.
    /// </summary>
    protected abstract void GenerateTile(T gen, in CETileGenContext ctx, ref CETileContent result);

    /// <summary>
    /// Optional per-chunk teardown.
    /// </summary>
    protected virtual void EndChunk(T gen)
    {
    }
}
