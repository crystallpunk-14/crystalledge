using Content.Server._CE.WorldGen;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.Generators;

/// <summary>
/// Fills every tile of the chunk — across all of its z-levels — with a single tile type,
/// and optionally spawns and anchors one entity on every tile (e.g. a solid stone-wall chunk).
/// </summary>
public sealed partial class CEFillChunkGenerator : CEChunkGeneratorBase<CEFillChunkGenerator>
{
    /// <summary>
    /// Tile placed on every cell of every z-level of the chunk.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ContentTileDefinition> FillTile;

    /// <summary>
    /// Entity spawned and anchored on every cell of every z-level (null == bare floor).
    /// </summary>
    [DataField]
    public EntProtoId? FillEntity;
}

/// <summary>
/// Executes <see cref="CEFillChunkGenerator"/>: for every z-level of the chunk, writes the fill
/// tile to every cell, then spawns + anchors the fill entity (if any), skipping player-modified tiles.
/// </summary>
public sealed partial class CEFillChunkGeneratorSystem : CEChunkGeneratorSystem<CEFillChunkGenerator>
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;

    protected override void Generate(ref CEChunkGenEvent<CEFillChunkGenerator> args)
    {
        var gen = args.Generator;
        var a = args.Args;

        var tile = new Tile(_tileDef[gen.FillTile].TileId);
        var levelTiles = new List<(Vector2i, Tile)>(CEWorldGenSystem.ChunkSize * CEWorldGenSystem.ChunkSize);

        for (var level = 0; level < a.LevelGrids.Count; level++)
        {
            var grid = a.LevelGrids[level];
            var modified = a.ModifiedTilesPerLevel[level];

            // Tiles first (batched per level).
            levelTiles.Clear();
            for (var x = 0; x < CEWorldGenSystem.ChunkSize; x++)
            {
                for (var y = 0; y < CEWorldGenSystem.ChunkSize; y++)
                {
                    var idx = new Vector2i(a.ChunkOriginTile.X + x, a.ChunkOriginTile.Y + y);
                    if (modified.Contains(idx))
                        continue;

                    levelTiles.Add((idx, tile));
                    a.GeneratedTiles.Add((level, idx, tile));
                }
            }

            _map.SetTiles(grid.Owner, grid.Comp, levelTiles);

            // Entities.
            if (gen.FillEntity is not { } proto)
                continue;

            foreach (var (idx, _) in levelTiles)
            {
                var coords = _map.GridTileToLocal(grid.Owner, grid.Comp, idx);
                var ent = Spawn(proto, coords);

                var xform = Transform(ent);
                if (!xform.Anchored)
                    _transform.AnchorEntity((ent, xform), grid, idx);

                a.SpawnedEntities.Add((level, ent, idx));
            }
        }
    }
}
