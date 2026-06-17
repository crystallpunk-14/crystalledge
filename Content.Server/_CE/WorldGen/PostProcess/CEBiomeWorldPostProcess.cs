using Content.Shared._CE.WorldGen.Generators;
using Content.Shared._CE.WorldGen.PostProcess;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Shared.Map;

namespace Content.Server._CE.WorldGen.PostProcess;

/// <summary>
/// Per-tile worldgen post-process: overlays biome noise layers on top of generated tile content.
/// Supports <see cref="IBiomeLayer"/> tile, entity and decal layers — same data as used by the biome
/// system, but applied synchronously per-tile during chunk generation rather than as a post-async pass.
/// </summary>
public sealed partial class CEBiomeWorldPostProcess : CEWorldPostProcessLayerBase<CEBiomeWorldPostProcess>
{
    /// <summary>
    /// Ordered biome layers to evaluate per tile.
    /// </summary>
    [DataField(required: true)]
    public List<IBiomeLayer> Layers = new();

    /// <summary>
    /// Optional seed offset XOR-ed with the world seed before biome evaluation, allowing multiple
    /// instances to produce independent noise patterns.
    /// </summary>
    [DataField]
    public int SeedOffset;
}

/// <summary>
/// Executes <see cref="CEBiomeWorldPostProcess"/>: evaluates biome layers per tile and overwrites
/// tile/entity/decal content where the noise threshold is met.
/// </summary>
public sealed partial class CEBiomeWorldPostProcessSystem
    : CEWorldPostProcessSystem<CEBiomeWorldPostProcess>
{
    [Dependency] private SharedBiomeSystem _biome = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;

    protected override void Process(ref CEWorldPostProcessEvent<CEBiomeWorldPostProcess> ev)
    {
        var layer = ev.Layer;
        var ctx = ev.Ctx;
        var seed = ctx.Seed ^ layer.SeedOffset;
        var grid = ctx.Grid;

        // 1. Tile override — if biome wants a different tile, replace.
        if (_biome.TryGetTile(ctx.WorldTile, layer.Layers, seed, grid, out var biomeTile) &&
            biomeTile is { IsEmpty: false } bt)
        {
            if (_tileDef[bt.TypeId] is ContentTileDefinition def)
                ev.Content.Tile = def.ID;
        }

        // 2. Entity — only if generator produced none; biome entity layers fill empty tiles.
        if (ev.Content.Entity == null)
        {
            // Build a Tile value from the (possibly updated) tile proto for biome entity lookup.
            var tileForLookup = ev.Content.Tile is { } proto
                ? new Tile(_tileDef[proto].TileId)
                : new Tile();

            if (_biome.TryGetEntity(ctx.WorldTile, layer.Layers, tileForLookup, seed, grid, out var entityId))
                // Spawn anchored so ShouldKeepEntity can use IsDefault for unload diffing.
                // Biome entities that self-mutate on spawn must declare those fields in their prototype.
                ev.Content.Entity = new CEEntitySpec(entityId, Angle.Zero, true);
        }

        // 3. Decal — only if generator produced none; take the first decal from the list.
        if (ev.Content.Decal == null &&
            _biome.TryGetDecals(ctx.WorldTile, layer.Layers, seed, grid, out var decalList) &&
            decalList.Count > 0)
        {
            var (id, pos) = decalList[0];
            var offset = pos - ctx.WorldTile;
            ev.Content.Decal = new CEDecalSpec(id, offset, Angle.Zero);
        }
    }
}
