using System.Threading.Tasks;
using Content.Server.Decals;
using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.Procedural.PostProcess;

/// <summary>
/// Post-process layer: applies biome layers on top of existing dungeon tiles.
/// Supports all <see cref="IBiomeLayer"/> types by delegating to <see cref="BiomeSystem"/>.
/// </summary>
public sealed partial class CEBiomeApplyPostProcess : CEDungeonPostProcessLayer
{
    [DataField(required: true)]
    public List<IBiomeLayer> Layers = new();

    /// <summary>
    /// Seed offset for noise generation. If null, a random seed is chosen at runtime.
    /// </summary>
    [DataField]
    public int? Seed;

    public override async Task Execute(IEntityManager entMan, EntityUid mapUid, Func<ValueTask> suspend)
    {
        var postProcess = entMan.System<CEDungeonPostProcessSystem>();
        var biome = entMan.System<BiomeSystem>();
        var map = entMan.System<SharedMapSystem>();
        var decals = entMan.System<DecalSystem>();

        var seed = Seed ?? new Random().Next();
        var maps = postProcess.GetAllMaps(mapUid);
        var counter = 0;

        foreach (var uid in maps)
        {
            if (!entMan.TryGetComponent<MapGridComponent>(uid, out var grid))
                continue;

            var gridEnt = new Entity<MapGridComponent>(uid, grid);

            // Pass 1: BiomeTileLayer — set tiles (skip empty results to preserve existing tiles).
            foreach (var tileRef in map.GetAllTiles(uid, grid))
            {
                if (++counter % 500 == 0)
                    await suspend();

                if (!biome.TryGetTile(tileRef.GridIndices, Layers, seed, gridEnt, out var tile))
                    continue;

                if (tile.Value.IsEmpty)
                    continue;

                map.SetTile(uid, grid, tileRef.GridIndices, tile.Value);
            }

            // Pass 2: BiomeEntityLayer — spawn entities on matching tiles.
            foreach (var tileRef in map.GetAllTiles(uid, grid))
            {
                if (++counter % 500 == 0)
                    await suspend();

                if (biome.TryGetEntity(tileRef.GridIndices, Layers, tileRef.Tile, seed, gridEnt, out var entity))
                    entMan.SpawnEntity(entity, map.GridTileToLocal(uid, grid, tileRef.GridIndices));
            }

            // Pass 3: BiomeDecalLayer — place decals on matching tiles.
            foreach (var tileRef in map.GetAllTiles(uid, grid))
            {
                if (++counter % 200 == 0)
                    await suspend();

                if (!biome.TryGetDecals(tileRef.GridIndices, Layers, seed, gridEnt, out var decalList))
                    continue;

                foreach (var (id, pos) in decalList)
                    decals.TryAddDecal(id, new EntityCoordinates(uid, pos), out _);
            }
        }
    }
}
