/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;

    [PublicAPI]
    public Entity<CEZGridNetworkComponent> CreateGridNetwork()
    {
        var networkId = Guid.NewGuid().ToString("N");
        var ent  = Spawn();
        var comp = EnsureComp<CEZGridNetworkComponent>(ent);
        comp.NetworkId = networkId;
        Dirty(ent, comp);
        return (ent, comp);
    }

    [PublicAPI]
    public bool TryAddGridToNetwork(Entity<CEZGridNetworkComponent> network, EntityUid grid)
    {
        if (!_mapGridQuery.HasComp(grid))
        {
            Log.Error($"CEZGridLinker: {grid} is not a MapGrid.");
            return false;
        }

        if (TryGetGridNetwork(grid, out var existing))
        {
            Log.Warning($"CEZGridLinker: grid {grid} already in network {existing.Owner}.");
            return false;
        }

        network.Comp.Grids.Add(grid);
        Dirty(network);

        var zGridComp = EnsureComp<CEZGridComponent>(grid);
        zGridComp.NetworkId = network.Comp.NetworkId;
        zGridComp.Network   = network.Owner;
        Dirty(grid, zGridComp);

        var ev = new CEGridLinkedEvent(network.Owner);
        RaiseLocalEvent(grid, ref ev);
        return true;
    }

    [PublicAPI]
    public bool TryRemoveGridFromNetwork(EntityUid grid)
    {
        if (!TryGetGridNetwork(grid, out var network))
            return false;

        network.Comp.Grids.Remove(grid);
        RemComp<CEZGridComponent>(grid);

        if (!TerminatingOrDeleted(network.Owner))
            Dirty(network);

        var ev = new CEGridUnlinkedEvent(network.Owner);
        RaiseLocalEvent(grid, ref ev);

        if (network.Comp.Grids.Count == 0 && !TerminatingOrDeleted(network.Owner))
            QueueDel(network);

        return true;
    }

    /// <summary>
    /// Explicit teardown: clears membership on all grids and queues the manager for deletion.
    /// Does not raise per-grid events — use <see cref="TryRemoveGridFromNetwork"/> for that.
    /// </summary>
    [PublicAPI]
    public void DeleteGridNetwork(Entity<CEZGridNetworkComponent> network)
    {
        foreach (var grid in network.Comp.Grids.ToList())
        {
            if (_zgridQuery.HasComp(grid))
                RemComp<CEZGridComponent>(grid);
        }
        QueueDel(network);
    }
}
