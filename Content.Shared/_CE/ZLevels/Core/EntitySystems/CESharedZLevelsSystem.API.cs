/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    /// <summary>
    /// Checks whether the map is in the zLevels network. If so, returns true and the current depth + Entity of the current zLevels network.
    /// </summary>
    [PublicAPI]
    public bool TryGetZNetwork(Entity<CEZLevelMapComponent?> entity, out Entity<CEZLevelsNetworkComponent> zLevel)
    {
        zLevel = default;

        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        if (TerminatingOrDeleted(entity.Comp.NetworkUid))
        {
            Log.Warning($"Trying access to terminated z-network, map: {entity}, outdated network uid: {entity.Comp.NetworkUid}");
            return false;
        }

        if (!TryComp<CEZLevelsNetworkComponent>(entity.Comp.NetworkUid, out var zNetworkComponent))
        {
            Log.Warning($"Trying access to z-network without component??? WHY?! map: {entity}, network uid: {entity.Comp.NetworkUid}");
            return false;
        }

        zLevel = new Entity<CEZLevelsNetworkComponent>(entity.Comp.NetworkUid, zNetworkComponent);
        return true;
    }

    [PublicAPI]
    public bool TryMapOffset(Entity<CEZLevelMapComponent?> entity, int offset, out Entity<CEZLevelMapComponent> outputMapUid)
    {
        outputMapUid = default;

        if (MapOffset(entity, offset) is not { } result)
            return false;

        outputMapUid = result;
        return true;
    }

    [PublicAPI]
    public Entity<CEZLevelMapComponent>? MapOffset(Entity<CEZLevelMapComponent?> entity, int offset)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return null;

        // Often we use 1 or -1 for getting maps
        // Because we process this separated for performance boost
        switch (offset)
        {
            case 1 when entity.Comp.MapAbove is not null:
                return _zMapQuery.TryGetComponent(entity.Comp.MapAbove.Value, out var componentAbove)
                    ? new Entity<CEZLevelMapComponent>(entity.Comp.MapAbove.Value, componentAbove)
                    : null;

            case -1 when entity.Comp.MapBelow is not null:
                return _zMapQuery.TryGetComponent(entity.Comp.MapBelow.Value, out var componentBelow)
                    ? new Entity<CEZLevelMapComponent>(entity.Comp.MapBelow.Value, componentBelow)
                    : null;
        }

        if (!_zNetworkQuery.TryComp(entity.Comp.NetworkUid, out var zLevelsNetworkComponent))
            return null;

        var requiredDepth = entity.Comp.Depth + offset;
        if (!zLevelsNetworkComponent.ZLevels.TryGetValue(requiredDepth, out var targetId))
            return null;

        if (!_zMapQuery.TryComp(targetId, out var zLevelMapComponent))
            return null;

        return (targetId.Value, zLevelMapComponent);
    }

    [PublicAPI]
    public bool TryMapUp(Entity<CEZLevelMapComponent?> entity, out Entity<CEZLevelMapComponent> aboveMapUid)
    {
        return TryMapOffset(entity, 1, out aboveMapUid);
    }

    [PublicAPI]
    public bool TryMapDown(Entity<CEZLevelMapComponent?> entity, out Entity<CEZLevelMapComponent> belowMapUid)
    {
        return TryMapOffset(entity, -1, out belowMapUid);
    }

    /// <summary>
    /// Returns a list of all maps above the specified map. The closest map at the top is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsAbove(Entity<CEZLevelMapComponent> mapUid)
    {
        if (!_zNetworkQuery.TryComp(mapUid.Comp.NetworkUid, out var networkComp) || mapUid.Comp.Depth >= networkComp.SortedMax)
            return new List<EntityUid>();

        var startIndex = mapUid.Comp.Depth < networkComp.SortedMin
            ? 0
            : mapUid.Comp.Depth - networkComp.SortedMin + 1;

        var result = new List<EntityUid>();
        for (var i = startIndex; i < networkComp.SortedZLevels.Count; i++)
        {
            var entity = networkComp.SortedZLevels[i];

            if (entity != EntityUid.Invalid && _zMapQuery.HasComp(entity))
                result.Add(entity);
        }

        return result;
    }

    /// <summary>
    /// Returns a list of all maps below the specified map. The closest map at the bottom is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsBelow(Entity<CEZLevelMapComponent> mapUid)
    {
        var result = new List<EntityUid>();
        if (!_zNetworkQuery.TryComp(mapUid.Comp.NetworkUid, out var zLevelsNetworkComponent))
            return result;

        var dept = mapUid.Comp.Depth;
        foreach (var mapEntry in zLevelsNetworkComponent.SortedZLevels)
        {
            if (_zMapQuery.TryComp(mapEntry, out var zComp) && zComp.Depth < dept)
                result.Add(mapEntry);
        }

        return result;
    }

    [PublicAPI]
    public bool IsEmptyAtCoordinates(EntityCoordinates coords, out Entity<CEZLevelMapComponent> belowMap)
    {
        belowMap = default;

        var mapUid = _transform.GetMapId(coords);
        if (mapUid == MapId.Nullspace)
            return false;

        var mapEntity = _map.GetMap(mapUid);
        if (!_zMapQuery.TryComp(mapEntity, out var zMapComp))
            return false;

        if (!TryMapDown((mapEntity, zMapComp), out belowMap))
            return false;

        if (!TryComp<MapGridComponent>(mapEntity, out var mapGridComponent))
            return true;

        var tileIndices = _map.LocalToTile(mapEntity, mapGridComponent, coords);
        var tile = _map.GetTileRef(mapEntity, mapGridComponent, tileIndices);

        return tile.Tile.IsEmpty;
    }

}
