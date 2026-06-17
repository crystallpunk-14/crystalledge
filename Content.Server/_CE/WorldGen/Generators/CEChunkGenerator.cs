using Content.Server.Decals;
using Content.Shared._CE.WorldGen.Generators;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.Generators;

/// <summary>
/// Abstract base system for handling chunk generators. Owns the per-tile loop: for every tile of every
/// z-level it asks the concrete generator (via <see cref="GenerateTile"/>) what to place, then writes
/// tiles (batched per level), spawns + anchors entities and adds decals, recording everything into the
/// args output lists for unload bookkeeping.
///
/// The generator never touches the grid, so it is structurally impossible to write outside the chunk.
///
/// Per-chunk scratch may be stored in <see cref="BeginChunk"/> on system fields: chunk loading is
/// synchronous and single-threaded (see <c>CEWorldGenSystem</c>), so only one chunk is ever in
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
