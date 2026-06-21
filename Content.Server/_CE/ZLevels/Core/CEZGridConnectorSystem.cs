/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>
/// Universal z-grid network recalculator driven by <see cref="CEZGridConnectorComponent"/>.
/// Sets a dirty flag on any topology event and runs a single recalculation pass per dirty cycle.
/// Network membership is always fully derived from the set of active connector entities.
/// </summary>
public sealed partial class CEZGridConnectorSystem : EntitySystem
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    [Dependency] private EntityQuery<CEZGridComponent> _zgridQuery = default!;
    [Dependency] private EntityQuery<CEZGridNetworkComponent> _zgridNetworkQuery = default!;

    private bool _dirty;

    /// <summary>Schedules a network recalculation on the next tick.</summary>
    public void MarkDirty()
    {
        _dirty = true;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZGridConnectorComponent, MapInitEvent>(OnConnectorMapInit);
        SubscribeLocalEvent<CEZGridConnectorComponent, AnchorStateChangedEvent>(OnConnectorAnchorChanged);
        SubscribeLocalEvent<CEZGridConnectorComponent, EntityTerminatingEvent>(OnConnectorTerminating);
        SubscribeLocalEvent<MapGridComponent, EntityTerminatingEvent>(OnGridTerminating);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);

        SubscribeLocalEvent<CEZGridNetworkComponent, ComponentShutdown>(OnGridNetworkShutdown);
    }

    private void OnConnectorMapInit(Entity<CEZGridConnectorComponent> ent, ref MapInitEvent args)
    {
        _dirty = true;
    }

    private void OnConnectorAnchorChanged(Entity<CEZGridConnectorComponent> ent, ref AnchorStateChangedEvent args)
    {
        _dirty = true;
    }

    private void OnConnectorTerminating(Entity<CEZGridConnectorComponent> ent, ref EntityTerminatingEvent args)
    {
        _dirty = true;
    }

    private void OnGridTerminating(Entity<MapGridComponent> ent, ref EntityTerminatingEvent args)
    {
        _dirty = true;
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        _dirty = true;
    }

    private void OnGridSplit(ref GridSplitEvent ev)
    {
        _dirty = true;
    }

    private void OnGridNetworkShutdown(Entity<CEZGridNetworkComponent> ent, ref ComponentShutdown args)
    {
        // Safety cleanup for external network deletions. Grids removed by the recalculator
        // (via DeleteGridNetwork → RemComp<CEZGridComponent>) will not have CEZGridComponent
        // anymore, so the TryComp check below skips them safely.
        foreach (var grid in ent.Comp.Grids.ToList())
        {
            if (!_zgridQuery.TryComp(grid, out var gc) || gc.Network != ent.Owner)
                continue;
            _zLevels.TryRemoveGridFromNetwork(grid);
        }
    }

    public override void Update(float frameTime)
    {
        if (!_dirty)
            return;
        _dirty = false;
        RecalculateGridNetworks();
    }

    // ── Recalculation ─────────────────────────────────────────────────────────

    private void RecalculateGridNetworks()
    {
        var desired = ComputeDesiredComponents();

        // Snapshot existing networks: uid → grid set copy
        var existing = new Dictionary<EntityUid, HashSet<EntityUid>>();
        var nq = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (nq.MoveNext(out var uid, out var nc))
        {
            existing[uid] = new HashSet<EntityUid>(nc.Grids);
        }

        var processedNets = new HashSet<EntityUid>();

        foreach (var component in desired)
        {
            // Exact set match → keep as-is, no events fired
            var matchNet = EntityUid.Invalid;
            foreach (var (netUid, grids) in existing)
            {
                if (!grids.SetEquals(component))
                    continue;
                matchNet = netUid;
                break;
            }

            if (matchNet.IsValid())
            {
                processedNets.Add(matchNet);
                continue;
            }

            // Dissolve all networks that overlap with this component (handles merges)
            foreach (var (netUid, grids) in existing)
            {
                if (processedNets.Contains(netUid) || !grids.Overlaps(component))
                    continue;
                if (_zgridNetworkQuery.TryComp(netUid, out var nc))
                    _zLevels.DeleteGridNetwork((netUid, nc));
                processedNets.Add(netUid);
            }

            // Create the correct network for this component
            var newNet = _zLevels.CreateGridNetwork();
            foreach (var grid in component)
            {
                _zLevels.TryAddGridToNetwork(newNet, grid);
            }

            processedNets.Add(newNet.Owner);
        }

        // Dissolve networks with no corresponding component
        foreach (var (netUid, _) in existing)
        {
            if (processedNets.Contains(netUid))
                continue;
            if (_zgridNetworkQuery.TryComp(netUid, out var nc))
                _zLevels.DeleteGridNetwork((netUid, nc));
        }
    }

    private List<HashSet<EntityUid>> ComputeDesiredComponents()
    {
        var adj = new Dictionary<EntityUid, HashSet<EntityUid>>();

        var lq = EntityQueryEnumerator<CEZGridConnectorComponent, TransformComponent>();
        while (lq.MoveNext(out var connectorUid, out _, out var xform))
        {
            if (!xform.Anchored || xform.GridUid == null || xform.MapUid == null)
                continue;

            var lower = xform.GridUid.Value;
            if (!TryComp<CEZLevelMapComponent>(xform.MapUid.Value, out var zMap))
                continue;
            if (!_zLevels.TryMapUp((xform.MapUid.Value, zMap), out var aboveMap))
                continue;

            var worldPos = _transform.GetWorldPosition(connectorUid);
            if (!_mapManager.TryFindGridAt(aboveMap.Owner, worldPos, out var upper, out var upperGrid))
                continue;
            if (upper == lower)
                continue;

            // TryFindGridAt matches by AABB — verify the tile at this position actually exists
            if (!_mapSystem.TryGetTileRef(upper, upperGrid, worldPos, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            if (!adj.TryGetValue(lower, out var ln))
                adj[lower] = ln = new HashSet<EntityUid>();
            if (!adj.TryGetValue(upper, out var un))
                adj[upper] = un = new HashSet<EntityUid>();
            ln.Add(upper);
            un.Add(lower);
        }

        // BFS → connected components
        var visited = new HashSet<EntityUid>();
        var result = new List<HashSet<EntityUid>>();

        foreach (var start in adj.Keys)
        {
            if (!visited.Add(start))
                continue;

            var comp = new HashSet<EntityUid>();
            var queue = new Queue<EntityUid>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                comp.Add(node);
                if (!adj.TryGetValue(node, out var neighbors))
                    continue;

                foreach (var neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            result.Add(comp);
        }

        return result;
    }
}
