using System.Linq;
using System.Numerics;
using Content.Server.NPC.Pathfinding;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Pathfinding;

/// <summary>
/// Maintains persistent cross-Z pathfinding portals at climbable ramps, turning a Z-network
/// into one connected A* graph, and answers the steering follower's "which way across the seam?"
/// query. Replaces the retired ladders cache.
/// </summary>
public sealed class CEZPortalSystem : EntitySystem
{
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private readonly EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private readonly EntityQuery<CEZLevelMapComponent> _zMapQuery = default!;
    [Dependency] private readonly EntityQuery<CEZPortalComponent> _portalQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelHighGroundComponent, MapInitEvent>(OnRampInit);
        SubscribeLocalEvent<CEZLevelHighGroundComponent, ComponentShutdown>(OnRampShutdown);
        SubscribeLocalEvent<CEZLevelHighGroundComponent, AnchorStateChangedEvent>(OnRampAnchorChanged);
        // Use MapComponent (not CEZLevelMapComponent) to avoid conflicting with CEZLevelMappingSystem.
        SubscribeLocalEvent<MapComponent, CEMapAddedIntoZNetworkEvent>(OnMapAddedToNetwork);
    }

    private void OnRampInit(Entity<CEZLevelHighGroundComponent> ent, ref MapInitEvent args)
    {
        TryCreateRampPortal(ent);
    }

    private void OnRampShutdown(Entity<CEZLevelHighGroundComponent> ent, ref ComponentShutdown args)
    {
        RemoveRampPortal(ent);
    }

    private void OnRampAnchorChanged(Entity<CEZLevelHighGroundComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            TryCreateRampPortal(ent);
        else
            RemoveRampPortal(ent);
    }

    // A ramp may be anchored before the map above joins the network; retry when the network grows.
    private void OnMapAddedToNetwork(Entity<MapComponent> ent, ref CEMapAddedIntoZNetworkEvent args)
    {
        var query = EntityQueryEnumerator<CEZLevelHighGroundComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ramp, out var xform))
        {
            if (xform.MapUid == ent.Owner)
                TryCreateRampPortal((uid, ramp));
        }
    }

    /// <summary>
    /// True if this high ground is a ramp that can be climbed between floors: its curve dips low
    /// enough to step onto from the current floor and rises to (or past) the level boundary.
    /// Excludes flat ledges such as the default [1.05, 1.05] wall-top.
    /// </summary>
    private static bool IsClimbableRamp(CEZLevelHighGroundComponent comp)
    {
        return comp.HeightCurve.Count >= 2 && comp.HeightCurve.Min() <= 0.9f && comp.HeightCurve.Max() >= 1f;
    }

    private void TryCreateRampPortal(Entity<CEZLevelHighGroundComponent> ent)
    {
        if (!IsClimbableRamp(ent.Comp))
            return;

        if (!_xformQuery.TryGetComponent(ent, out var xform) || !xform.Anchored)
            return;

        var gridUid = xform.GridUid;
        if (gridUid == null || !_gridQuery.TryGetComponent(gridUid.Value, out var grid))
            return;

        // Resolve the map above this ramp's map within its Z-network.
        var mapUid = xform.MapUid;
        if (mapUid == null || !_zMapQuery.TryGetComponent(mapUid.Value, out var zMap) || zMap.MapAbove is not { } aboveMap)
            return;

        if (!_gridQuery.TryGetComponent(aboveMap, out var aboveGrid))
            return;

        // Don't create twice.
        var portalComp = EnsureComp<CEZPortalComponent>(gridUid.Value);
        if (portalComp.Ramps.ContainsKey(ent.Owner))
            return;

        var rampWorld = _transform.GetWorldPosition(xform);
        var rampTile = _map.WorldToTile(gridUid.Value, grid, rampWorld);
        var downhillDir = xform.LocalRotation.GetCardinalDir(); // ramp faces downhill
        var uphillDir = downhillDir.GetOpposite();

        // Lower endpoint: the clear floor on the low (downhill) side of the ramp.
        var lowApproachTile = rampTile + downhillDir.ToIntVec();
        var lowApproach = _map.GridTileToLocal(gridUid.Value, grid, lowApproachTile);

        // Upper endpoint: the tile directly above the ramp (where the climber lands), on the map above.
        var landing = _map.GridTileToLocal(aboveMap, aboveGrid, rampTile);

        if (!_pathfinding.TryCreatePortal(lowApproach, landing, out var handle))
            return;

        portalComp.Ramps[ent.Owner] = new CEZRampPortal
        {
            Handle = handle,
            RampTile = rampTile,
            UphillDir = uphillDir,
        };
    }

    private void RemoveRampPortal(Entity<CEZLevelHighGroundComponent> ent)
    {
        if (!_xformQuery.TryGetComponent(ent, out var xform))
            return;

        var gridUid = xform.GridUid;
        if (gridUid == null || !_portalQuery.TryGetComponent(gridUid.Value, out var portalComp))
            return;

        if (!portalComp.Ramps.Remove(ent.Owner, out var ramp))
            return;

        _pathfinding.RemovePortal(ramp.Handle);
    }

    /// <summary>
    /// At a cross-map path seam, returns the world-space direction to keep walking so the existing
    /// vertical physics carries the NPC onto the ramp and across the level boundary.
    /// Ascend (target on the map above): walk uphill. Descend (target on the map below): walk downhill.
    /// </summary>
    [PublicAPI]
    public bool TryGetZSeamDirection(EntityUid npc, EntityCoordinates targetNode, out Vector2 worldDir)
    {
        worldDir = Vector2.Zero;

        if (!_xformQuery.TryGetComponent(npc, out var xform) || xform.MapUid is not { } ourMap)
            return false;

        if (!_zMapQuery.TryGetComponent(ourMap, out var ourZMap))
            return false;

        var targetMap = _transform.GetMap(targetNode);
        if (targetMap == null)
            return false;

        var ourWorld = _transform.GetWorldPosition(xform);

        // Ascend: ramp is on OUR grid; walk uphill.
        if (targetMap == ourZMap.MapAbove)
        {
            if (!TryGetNearestRamp(ourMap, ourWorld, out var ramp))
                return false;
            worldDir = ramp.UphillDir.ToVec();
            return true;
        }

        // Descend: ramp is on the grid BELOW us; walk downhill (toward the ramp).
        if (ourZMap.MapBelow is { } belowMap && targetMap == belowMap)
        {
            if (!TryGetNearestRamp(belowMap, ourWorld, out var ramp))
                return false;
            worldDir = ramp.UphillDir.GetOpposite().ToVec();
            return true;
        }

        return false;
    }

    private bool TryGetNearestRamp(EntityUid gridUid, Vector2 worldPos, out CEZRampPortal ramp)
    {
        ramp = default;

        if (!_portalQuery.TryGetComponent(gridUid, out var portalComp) ||
            !_gridQuery.TryGetComponent(gridUid, out var grid))
            return false;

        var originTile = _map.WorldToTile(gridUid, grid, worldPos);
        var bestDistSq = float.MaxValue;
        var found = false;

        foreach (var candidate in portalComp.Ramps.Values)
        {
            var dx = candidate.RampTile.X - originTile.X;
            var dy = candidate.RampTile.Y - originTile.Y;
            var distSq = dx * dx + dy * dy;

            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            ramp = candidate;
            found = true;
        }

        return found;
    }
}
