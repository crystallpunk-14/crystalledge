/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.ZLevels.Roof;
using Content.Shared.Light.Components;
using Content.Shared.Maps;

namespace Content.Server._CE.ZLevels.Roof;

/// <inheritdoc/>
public sealed partial class CERoofSystem : CESharedRoofSystem
{
    [Dependency] private EntityQuery<CEZGridComponent> _zgridQuery = default!;
    [Dependency] private EntityQuery<CEZGridNetworkComponent> _zGridNetworkQuery = default!;

    private readonly HashSet<Vector2i> _roofMap = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZMapNetworkComponent, CEZLevelMapNetworkUpdatedEvent>(OnNetworkUpdated);

        SubscribeLocalEvent<CEZGridComponent, MapInitEvent>(OnZGridMapInit);
        SubscribeLocalEvent<CEZLevelMapRoofComponent, CEGridAddedIntoZNetworkEvent>(OnZGridLinked);
        SubscribeLocalEvent<CEZLevelMapRoofComponent, CEGridRemovedFromZNetworkEvent>(OnZGridUnlinked);
    }

    private void OnNetworkUpdated(Entity<CEZMapNetworkComponent> ent, ref CEZLevelMapNetworkUpdatedEvent args)
    {
        RecalculateNetworkRoofs(ent);
    }

    private void OnZGridMapInit(Entity<CEZGridComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<CEZLevelMapRoofComponent>(ent.Owner);
    }

    private void OnZGridLinked(Entity<CEZLevelMapRoofComponent> ent, ref CEGridAddedIntoZNetworkEvent args)
    {
        if (!_zgridQuery.TryComp(ent.Owner, out var zGrid))
            return;
        if (!_zGridNetworkQuery.TryComp(zGrid.Network, out var network))
            return;
        RecalculateZGridNetworkRoofs((zGrid.Network, network));
    }

    private void OnZGridUnlinked(Entity<CEZLevelMapRoofComponent> ent, ref CEGridRemovedFromZNetworkEvent args)
    {
        RemCompDeferred<CEZLevelMapRoofComponent>(ent.Owner);
        RemCompDeferred<RoofComponent>(ent.Owner);

        if (_zGridNetworkQuery.TryComp(args.Network, out var network))
            RecalculateZGridNetworkRoofs((args.Network, network));
    }

    protected override void OnChildGridTileChanged(Entity<CEZLevelMapRoofComponent> ent, ref TileChangedEvent args)
    {
        if (!_zgridQuery.TryComp(ent.Owner, out var zGrid))
            return;
        if (!_zGridNetworkQuery.TryComp(zGrid.Network, out var network))
            return;
        RecalculateZGridNetworkRoofs((zGrid.Network, network));
    }

    public void RecalculateNetworkRoofs(Entity<CEZMapNetworkComponent> network)
    {
        _roofMap.Clear();

        List<EntityUid> sortedMaps = new();
        foreach (var mapUid in network.Comp.ZLevels
                     .OrderByDescending(kv => kv.Key) // depth sorting
                     .Select(kv => kv.Value)
                     .Where(uid => uid.HasValue)
                     .Select(uid => uid!.Value))
        {
            sortedMaps.Add(mapUid);
        }

        foreach (var map in sortedMaps)
        {
            if (!GridQuery.TryComp(map, out var mapGrid))
                continue;

            var enumerator = Map.GetAllTilesEnumerator(map, mapGrid);
            var roofComp = EnsureComp<RoofComponent>(map);

            while (enumerator.MoveNext(out var tileRef))
            {
                Roof.SetRoof((map, mapGrid, roofComp), tileRef.Value.GridIndices, _roofMap.Contains(tileRef.Value.GridIndices));

                var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Value.Tile.TypeId];

                if (!tileDef.Transparent)
                    _roofMap.Add(tileRef.Value.GridIndices);
            }
        }
    }

    public void RecalculateZGridNetworkRoofs(Entity<CEZGridNetworkComponent> network)
    {
        _roofMap.Clear();

        var sorted = network.Comp.Grids
            .Select(g => (Grid: g, Depth: ZLevel.TryGetGridZDepth(g)))
            .Where(x => x.Depth.HasValue)
            .OrderByDescending(x => x.Depth!.Value);

        foreach (var (gridUid, _) in sorted)
        {
            if (!GridQuery.TryComp(gridUid, out var grid))
                continue;

            var roofComp = EnsureComp<RoofComponent>(gridUid);
            var enumerator = Map.GetAllTilesEnumerator(gridUid, grid);

            while (enumerator.MoveNext(out var tileRef))
            {
                var worldTile = ZLevel.GridTileToWorldTile(gridUid, grid, tileRef.Value.GridIndices);
                Roof.SetRoof((gridUid, grid, roofComp), tileRef.Value.GridIndices,
                             _roofMap.Contains(worldTile));

                var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Value.Tile.TypeId];
                if (!tileDef.Transparent)
                    _roofMap.Add(worldTile);
            }
        }
    }
}
