using System.Threading.Tasks;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.Procedural.PostProcess;

/// <summary>
/// Post-process layer: randomises tile variants on every tile of every z-level map.
/// Equivalent to the <c>znetwork-variantize</c> admin command.
/// </summary>
public sealed partial class CETileVariantizePostProcess : CEDungeonPostProcessLayer
{
    public override async Task Execute(IEntityManager entMan, EntityUid mapUid, Func<ValueTask> suspend)
    {
        var postProcess = entMan.System<CEDungeonPostProcessSystem>();
        var map = entMan.System<SharedMapSystem>();
        var tile = entMan.System<TileSystem>();
        var turf = entMan.System<TurfSystem>();

        var maps = postProcess.GetAllMaps(mapUid);
        var counter = 0;

        foreach (var uid in maps)
        {
            if (!entMan.TryGetComponent<MapGridComponent>(uid, out var grid))
                continue;

            foreach (var tileRef in map.GetAllTiles(uid, grid))
            {
                if (++counter % 1000 == 0)
                    await suspend();

                var def = turf.GetContentTileDefinition(tileRef);
                var newTile = new Tile(
                    tileRef.Tile.TypeId,
                    tileRef.Tile.Flags,
                    tile.PickVariant(def),
                    tileRef.Tile.RotationMirroring);
                map.SetTile(uid, grid, tileRef.GridIndices, newTile);
            }
        }
    }
}
