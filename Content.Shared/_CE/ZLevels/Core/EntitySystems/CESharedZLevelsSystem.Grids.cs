/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using JetBrains.Annotations;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] protected EntityQuery<CEZGridComponent> _zgridQuery = default!;
    [Dependency] protected EntityQuery<CEZGridNetworkComponent> _zgridNetworkQuery = default!;

    /// <summary>
    /// Cache-first lookup: checks <see cref="CEZGridComponent.Network"/> first,
    /// falls back to a NetworkId string scan and updates the cache on a miss.
    /// </summary>
    [PublicAPI]
    public bool TryGetGridNetwork(EntityUid grid, out Entity<CEZGridNetworkComponent> network)
    {
        network = default;

        if (!_zgridQuery.TryComp(grid, out var zGridComp) || zGridComp.NetworkId == string.Empty)
            return false;

        // Fast path
        if (zGridComp.Network.IsValid() && _zgridNetworkQuery.TryComp(zGridComp.Network, out var cached))
        {
            network = (zGridComp.Network, cached);
            return true;
        }

        // Slow path — scan by NetworkId, update cache on hit
        var q = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (q.MoveNext(out var uid, out var nc))
        {
            if (nc.NetworkId != zGridComp.NetworkId)
                continue;
            zGridComp.Network = uid;
            network = (uid, nc);
            return true;
        }

        return false;
    }
}

/// <summary>
/// Directed at a grid entity when it is added to a z-grid network by <see cref="CEZGridConnectorSystem"/>.
/// </summary>
[ByRefEvent]
public readonly struct CEGridLinkedEvent(EntityUid network)
{
    public readonly EntityUid Network = network;
}

/// <summary>
/// Directed at a grid entity when it is removed from a z-grid network,
/// either by the recalculator or by external network deletion.
/// </summary>
[ByRefEvent]
public readonly struct CEGridUnlinkedEvent(EntityUid network)
{
    public readonly EntityUid Network = network;
}
