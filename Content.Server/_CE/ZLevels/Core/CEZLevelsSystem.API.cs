/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Diagnostics.CodeAnalysis;
using Content.Server._CE.PVS;
using Content.Shared._CE.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    /// <summary>
    /// Creates a new entity zLevelNetwork
    /// </summary>
    [PublicAPI]
    public Entity<CEZLevelsNetworkComponent> CreateZNetwork(ComponentRegistry? components = null, string? name = null)
    {
        var ent = Spawn();

        var zLevel = EnsureComp<CEZLevelsNetworkComponent>(ent);
        EnsureComp<CEPvsOverrideComponent>(ent);

        zLevel.Components = components ?? new ComponentRegistry();

        _meta.SetEntityName(ent, name ?? $"ZNetwork {ent.Id}");

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


        // Give the map a sensible name
        _meta.SetEntityName(mapUid, $" {MetaData(network).EntityName}: [{depth}]");

        RaiseLocalEvent(mapUid, new CEMapAddedIntoZNetworkEvent(network, depth));
        RaiseLocalEvent(network, new CEZLevelNetworkUpdatedEvent());

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

        return success;
    }

    /// <summary>
    /// Attempts to load the specified map resource and add it to the z-network at the specified depth.
    /// </summary>
    public bool TryAddMapIntoZNetwork(
        Entity<CEZLevelsNetworkComponent> network,
        ResPath path,
        int depth,
        [NotNullWhen(true)] out Entity<MapComponent>? outMap)
    {
        outMap = null;

        if (!_mapLoader.TryLoadMap(path, out var mapEnt, out _))
        {
            Log.Error($"Failed to load map {path} for ZLevelNetwork {network} at depth {depth}!");
            return false;
        }
        outMap = mapEnt;

        return TryAddMapIntoZNetwork(network, mapEnt.Value, depth);
    }

    /// <summary>
    /// Attempts to load and add multiple maps (specified by resource paths) into the z-network.
    /// </summary>
    public bool TryAddMapsIntoZNetwork(Entity<CEZLevelsNetworkComponent> network, Dictionary<ResPath, int> maps)
    {
        var success = true;
        foreach (var (path, depth) in maps)
        {
            if (!_mapLoader.TryLoadMap(path, out var mapEnt, out _))
            {
                Log.Error($"Failed to load map {path} for ZLevelNetwork {network} at depth {depth}!");
                success = false;
                continue;
            }

            if (!TryAddMapIntoZNetwork(network, mapEnt.Value, depth))
                success = false;
        }

        return success;
    }

    // Default empty map used when ensuring map existence.
    public ResPath EmptyMap = new("/Maps/_CE/Empty.yml");

    /// <summary>
    /// Ensures there is a map one level above the input map. Returns the map EntityUid.
    /// </summary>
    public Entity<CEZLevelMapComponent> EnsureMapUp(Entity<CEZLevelMapComponent?> inputMapUid) => EnsureMapOffset(inputMapUid, 1);

    /// <summary>
    /// Ensures there is a map one level below the input map. Returns the map EntityUid.
    /// </summary>
    public Entity<CEZLevelMapComponent> EnsureMapDown(Entity<CEZLevelMapComponent?> inputMapUid) => EnsureMapOffset(inputMapUid, -1);

    /// <summary>
    /// Ensures there is a map at the specified offset from the input map (inputMap.Depth + offset).
    /// If the map doesn't exist, an empty map from <see cref="EmptyMap"/> will be loaded and added to the network.
    /// Returns the map EntityUid (or default(EntityUid) on failure).
    /// </summary>
    public Entity<CEZLevelMapComponent> EnsureMapOffset(Entity<CEZLevelMapComponent?> inputMapUid, int offset)
    {
        if (!Resolve(inputMapUid, ref inputMapUid.Comp, false))
        {
            Log.Error($"Failed to resolve CEZLevelMapComponent for entity {inputMapUid.Owner}!");
            return default;
        }

        // Try to find the network containing this map
        if (!TryGetZNetwork(inputMapUid, out var network))
        {
            Log.Error($"Failed to find ZLevelNetwork for map {inputMapUid.Owner}!");
            return default;
        }

        var targetDepth = inputMapUid.Comp.Depth + offset;

        // Check if map already exists at target depth
        if (network.Value.Comp.ZLevels.TryGetValue(targetDepth, out var existing) && existing.HasValue && Exists(existing) && TryComp<CEZLevelMapComponent>(existing.Value, out var zLevelMapComp))
            return (existing.Value, zLevelMapComp);

        // Load empty map
        if (!_mapLoader.TryLoadMap(EmptyMap, out var mapEnt, out _))
        {
            Log.Error($"Failed to load EmptyMap {EmptyMap} for ZLevelNetwork {network.Value} at depth {targetDepth}!");
            return default;
        }

        // Add to network
        if (!TryAddMapIntoZNetwork(network.Value, mapEnt.Value, targetDepth))
        {
            Log.Error($"Failed to add loaded EmptyMap {mapEnt.Value} into ZLevelNetwork {network.Value} at depth {targetDepth}.");
            return default;
        }

        return (mapEnt.Value, Comp<CEZLevelMapComponent>(mapEnt.Value));
    }

    public void InitializeAllZNetwork(Entity<CEZLevelsNetworkComponent> network)
    {
        foreach (var (_, mapUid) in network.Comp.ZLevels)
        {
            if (!TryComp<MapComponent>(mapUid, out var mapComp))
                continue;

            if (!_map.MapExists(mapComp.MapId))
                continue;

            if (_map.IsInitialized(mapComp.MapId))
                continue;

            _map.InitializeMap(mapComp.MapId);
        }
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
