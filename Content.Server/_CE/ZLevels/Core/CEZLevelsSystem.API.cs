/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.PVS;
using Content.Shared._CE.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    /// <summary>
    /// Initializes all uninitialized maps in the z-network.
    /// </summary>
    [PublicAPI]
    public void InitializeZNetwork(Entity<CEZLevelsNetworkComponent> network)
    {
        foreach (var (_, mapUid) in network.Comp.ZLevels)
        {
            if (!TryComp<MapComponent>(mapUid, out var mapComp))
            {
                Log.Error($"Map entity {mapUid} does not have MapComponent.");
                continue;
            }

            if (!_map.MapExists(mapComp.MapId))
            {
                Log.Error($"Map with ID {mapComp.MapId} does not exist.");
                continue;
            }

            if (_map.IsInitialized(mapComp.MapId))
            {
                Log.Debug($"Map with ID {mapComp.MapId} is already initialized.");
                continue;
            }

            _map.InitializeMap(mapComp.MapId);
            Log.Info($"Map with ID {mapComp.MapId} initialized.");
        }
    }

    /// <summary>
    /// Creates a new entity zLevelNetwork
    /// </summary>
    [PublicAPI]
    public Entity<CEZLevelsNetworkComponent> CreateZNetwork(ComponentRegistry? components = null)
    {
        var ent = Spawn();

        var zLevel = EnsureComp<CEZLevelsNetworkComponent>(ent);
        EnsureComp<CEPvsOverrideComponent>(ent);

        zLevel.Components = components ?? new ComponentRegistry();

        return (ent, zLevel);
    }

    /// <summary>
    /// Attempts to add the specified map to the zNetwork network at the specified depth
    /// </summary>
    private bool TryAddMapIntoZNetwork(Entity<CEZLevelsNetworkComponent> network, EntityUid mapUid, int depth)
    {
        if (network.Comp.ZLevels.ContainsKey(depth))
        {
            Log.Error($"Failed to add map {mapUid} to ZLevelNetwork {network}: This depth is already occupied.");
            return false;
        }

        if (TryGetZNetwork(mapUid, out var otherNetwork))
        {
            Log.Error($"Failed attempt to add map {mapUid} to ZLevelNetwork {network}: This map is already in another network {otherNetwork}.");
            return false;
        }

        if (network.Comp.ZLevels.ContainsValue(mapUid))
        {
            Log.Error($"Failed attempt to add map {mapUid} to ZLevelNetwork {network} at depth {depth}: This map is already in this network.");
            return false;
        }

        network.Comp.ZLevels.Add(depth, mapUid);
        var zlevel = EnsureComp<CEZLevelMapComponent>(mapUid);
        zlevel.Depth = depth;
        Dirty(network);
        Dirty(mapUid, zlevel);

        RaiseLocalEvent(mapUid, new CEMapAddedIntoZNetworkEvent(network, depth));

        return true;
    }

    public bool TryAddMapsIntoZNetwork(Entity<CEZLevelsNetworkComponent> network, Dictionary<EntityUid, int> maps)
    {
        var success = true;
        foreach (var (ent, depth) in maps)
        {
            if (!TryAddMapIntoZNetwork(network, ent, depth))
                success = false;
        }

        RaiseLocalEvent(network, new CEZLevelNetworkUpdatedEvent());

        return success;
    }

    /// <summary>
    /// Deletes a z-network: queues deletion of all maps in the network, then the network entity itself.
    /// </summary>
    [PublicAPI]
    public void DeleteZNetwork(EntityUid networkUid)
    {
        if (!TryComp<CEZLevelsNetworkComponent>(networkUid, out var zNet))
        {
            Log.Error($"CEZLevelsSystem: entity {networkUid} does not have CEZLevelsNetworkComponent.");
            return;
        }

        foreach (var (_, mapUid) in zNet.ZLevels)
        {
            if (mapUid != null)
                QueueDel(mapUid.Value);
        }

        QueueDel(networkUid);
    }
}

/// <summary>
/// Called on ZLevel Network Entity, when maps added or removed from network
/// </summary>
public sealed class CEZLevelNetworkUpdatedEvent : EntityEventArgs;

/// <summary>
/// Called on map, when it added to ZNetwork
/// </summary>
public sealed class CEMapAddedIntoZNetworkEvent(Entity<CEZLevelsNetworkComponent> network, int depth) : EntityEventArgs
{
    public Entity<CEZLevelsNetworkComponent> Network = network;
    public int Depth = depth;
}
