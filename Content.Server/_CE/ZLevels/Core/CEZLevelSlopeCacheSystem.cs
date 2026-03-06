using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels;

/// <summary>
/// Maintains a cache of slope (ramp) positions on each map grid for NPC navigation.
/// A slope is a <see cref="CEZLevelHighGroundComponent"/> entity whose HeightCurve transitions
/// from below 1.0 to at or above 1.0 — i.e. a ramp that can be walked up to transition Z-levels.
/// </summary>
public sealed class CEZLevelSlopeCacheSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<CEZLevelSlopeCacheComponent> _cacheQuery;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _cacheQuery = GetEntityQuery<CEZLevelSlopeCacheComponent>();

        SubscribeLocalEvent<CEZLevelHighGroundComponent, MapInitEvent>(OnSlopeMapInit);
        SubscribeLocalEvent<CEZLevelHighGroundComponent, ComponentShutdown>(OnSlopeShutdown);
        SubscribeLocalEvent<CEZLevelHighGroundComponent, AnchorStateChangedEvent>(OnSlopeAnchorChanged);
    }

    private void OnSlopeMapInit(EntityUid uid, CEZLevelHighGroundComponent comp, MapInitEvent args)
    {
        TryAddSlope(uid, comp);
    }

    private void OnSlopeShutdown(EntityUid uid, CEZLevelHighGroundComponent comp, ComponentShutdown args)
    {
        TryRemoveSlope(uid);
    }

    private void OnSlopeAnchorChanged(EntityUid uid, CEZLevelHighGroundComponent comp,
        ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            TryAddSlope(uid, comp);
        else
            TryRemoveSlope(uid);
    }

    /// <summary>
    /// Checks if the HighGround entity qualifies as a navigable slope (ramp),
    /// and if so, registers it in the cache on its parent grid.
    /// </summary>
    private void TryAddSlope(EntityUid uid, CEZLevelHighGroundComponent comp)
    {
        if (!IsNavigableSlope(comp))
            return;

        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return;

        if (!xform.Anchored)
            return;

        var gridUid = xform.GridUid;
        if (gridUid == null || !_gridQuery.TryGetComponent(gridUid.Value, out var grid))
            return;

        var cache = EnsureComp<CEZLevelSlopeCacheComponent>(gridUid.Value);
        var tilePos = _map.WorldToTile(gridUid.Value, grid, _transform.GetWorldPosition(xform));
        var dir = xform.LocalRotation.GetCardinalDir();

        cache.Slopes[tilePos] = new CECachedSlope
        {
            Entity = uid,
            Direction = dir,
        };
    }

    /// <summary>
    /// Removes a slope entity from the cache when it's destroyed, unanchored, or removed.
    /// </summary>
    private void TryRemoveSlope(EntityUid uid)
    {
        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return;

        var gridUid = xform.GridUid;
        if (gridUid == null || !_cacheQuery.TryGetComponent(gridUid.Value, out var cache))
            return;

        if (!_gridQuery.TryGetComponent(gridUid.Value, out var grid))
            return;

        var tilePos = _map.WorldToTile(gridUid.Value, grid, _transform.GetWorldPosition(xform));
        cache.Slopes.Remove(tilePos);
    }

    /// <summary>
    /// A slope is "navigable" (usable as a ramp) if its HeightCurve transitions from
    /// below 1.0 to at or above 1.0. Flat high-ground like wall tops [1.05, 1.05] are excluded.
    /// </summary>
    private static bool IsNavigableSlope(CEZLevelHighGroundComponent comp)
    {
        if (comp.HeightCurve.Count < 2)
            return false;

        var hasLow = false;
        var hasHigh = false;

        foreach (var h in comp.HeightCurve)
        {
            if (h < 1.0f)
                hasLow = true;
            if (h >= 1.0f)
                hasHigh = true;
        }

        return hasLow && hasHigh;
    }

    /// <summary>
    /// Finds the nearest cached slope on a given grid to the specified world position.
    /// </summary>
    /// <returns>True if a slope was found.</returns>
    public bool TryFindNearestSlope(
        EntityUid gridUid,
        Vector2 worldPos,
        float maxRange,
        out Vector2i slopeTilePos,
        out CECachedSlope slope)
    {
        slopeTilePos = default;
        slope = default;

        if (!_cacheQuery.TryGetComponent(gridUid, out var cache))
            return false;

        if (!_gridQuery.TryGetComponent(gridUid, out var grid))
            return false;

        var npcTile = _map.WorldToTile(gridUid, grid, worldPos);
        var maxRangeInt = (int)Math.Ceiling(maxRange);
        var bestDistSq = float.MaxValue;
        var found = false;

        foreach (var (tilePos, cachedSlope) in cache.Slopes)
        {
            // Quick Manhattan pre-filter
            var dx = Math.Abs(tilePos.X - npcTile.X);
            var dy = Math.Abs(tilePos.Y - npcTile.Y);
            if (dx > maxRangeInt || dy > maxRangeInt)
                continue;

            var distSq = (tilePos.X - npcTile.X) * (tilePos.X - npcTile.X)
                         + (tilePos.Y - npcTile.Y) * (tilePos.Y - npcTile.Y);

            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            slopeTilePos = tilePos;
            slope = cachedSlope;
            found = true;
        }

        return found;
    }
}
