using System.Numerics;
using Content.Server._CE.ZLevels;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Moves the NPC towards its current target entity.
/// Supports cross-Z-level navigation: if the target is on a different Z-level,
/// the NPC routes to the nearest slope and is teleported via <see cref="CESharedZLevelsSystem.TryMoveUp"/>
/// or <see cref="CESharedZLevelsSystem.TryMoveDown"/>.
/// After transitioning between maps, the GOAP system triggers a re-plan.
/// </summary>
public sealed partial class CEGOAPMoveToTargetAction : CEGOAPActionBase<CEGOAPMoveToTargetAction>
{
    /// <summary>
    /// How close the NPC needs to get to the target to consider the action complete.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// How far the target must move before re-registering the steering destination.
    /// Prevents constant pathfinding recalculation while still tracking moving targets.
    /// </summary>
    [DataField]
    public float ReregisterThreshold = 1.5f;

    /// <summary>
    /// Maximum search radius (in tiles) for finding slopes when cross-Z navigation is needed.
    /// </summary>
    [DataField]
    public float SlopeSearchRadius = 10f;
}

public sealed partial class CEGOAPMoveToTargetActionSystem : CEGOAPActionSystem<CEGOAPMoveToTargetAction>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly CEZLevelSlopeCacheSystem _slopeCache = default!;
    [Dependency] private readonly SharedTransformSystem _transformSys = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<NPCSteeringComponent> _steeringQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<CEZLevelMapComponent> _zMapQuery;

    /// <summary>
    /// Tracks NPCs navigating to a slope for ascent.
    /// Value = slope direction (downhill, as per GetCardinalDir convention).
    /// After <see cref="CESharedZLevelsSystem.TryMoveUp"/>,
    /// the NPC is shifted 0.25 tile in the UPHILL direction (= opposite of stored dir)
    /// so it lands on the floor of the upper map.
    /// </summary>
    private readonly Dictionary<EntityUid, Direction> _pendingAscent = new();

    /// <summary>
    /// Data for a pending descent transition.
    /// </summary>
    private record struct DescentData(Direction SlopeDir, EntityUid BelowMapUid);

    /// <summary>
    /// Tracks NPCs navigating to a descent point.
    /// After arrival, the NPC is forcibly moved to the stored below-map
    /// and shifted 0.75 tile in the DOWNHILL direction to land on the slope.
    /// </summary>
    private readonly Dictionary<EntityUid, DescentData> _pendingDescent = new();

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _steeringQuery = GetEntityQuery<NPCSteeringComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _zMapQuery = GetEntityQuery<CEZLevelMapComponent>();
    }

    protected override void OnActionStartup(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionStartupEvent<CEGOAPMoveToTargetAction> args)
    {
        RegisterSteering(ent, args.Action);
    }

    protected override void OnActionUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionUpdateEvent<CEGOAPMoveToTargetAction> args)
    {
        var target = GetTarget(ent.Comp, args.Action.TargetProviderKey);
        if (target == null)
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        if (!_xformQuery.TryGetComponent(target.Value, out var targetXform))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        if (!_xformQuery.TryGetComponent(ent, out var npcXform))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // If on different maps, we are doing cross-Z navigation — never report Finished directly.
        var sameMaps = npcXform.MapUid == targetXform.MapUid;

        if (_steeringQuery.TryComp(ent, out var steering))
        {
            // Re-register if target moved significantly (only for same-map direct nav)
            if (sameMaps
                && steering.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var delta)
                && delta > args.Action.ReregisterThreshold)
            {
                RegisterSteering(ent, args.Action);
            }

            switch (steering.Status)
            {
                case SteeringStatus.InRange:
                    if (sameMaps)
                    {
                        args.Status = CEGOAPActionStatus.Finished;
                        return;
                    }

                    // Reached slope destination — teleport between Z-levels
                    if (_pendingAscent.Remove(ent.Owner, out var ascentDir))
                    {
                        _zLevels.TryMoveUp(ent);
                        // ascentDir = downhill; shift UPHILL (opposite) to land on upper map floor
                        var pos = _transformSys.GetWorldPosition(ent);
                        _transformSys.SetWorldPosition(ent, pos + ascentDir.GetOpposite().ToVec() * 0.25f);
                        // ParentChanged triggers re-plan
                    }
                    else if (_pendingDescent.Remove(ent.Owner, out var descentData))
                    {
                        // Force move to the map below at the shifted position
                        if (TryComp<MapComponent>(descentData.BelowMapUid, out var belowMapComp))
                        {
                            var pos = _transformSys.GetWorldPosition(ent);
                            var newPos = pos + descentData.SlopeDir.ToVec() * 0.75f;
                            _transformSys.SetMapCoordinates(ent, new MapCoordinates(newPos, belowMapComp.MapId));
                        }
                        // ParentChanged triggers re-plan
                    }
                    else
                    {
                        // Shouldn't happen — re-register just in case
                        RegisterSteering(ent, args.Action);
                    }
                    break;
                case SteeringStatus.NoPath:
                    args.Status = CEGOAPActionStatus.Failed;
                    return;
            }
        }

        args.Status = CEGOAPActionStatus.Running;
    }

    protected override void OnActionShutdown(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionShutdownEvent<CEGOAPMoveToTargetAction> args)
    {
        _pendingAscent.Remove(ent.Owner);
        _pendingDescent.Remove(ent.Owner);
        _steering.Unregister(ent);
    }

    /// <summary>
    /// Determines the correct steering destination and registers with NPCSteeringSystem.
    /// For same-map targets, routes directly. For cross-Z targets, routes to a slope tile.
    /// </summary>
    private void RegisterSteering(Entity<CEGOAPComponent> ent, CEGOAPMoveToTargetAction action)
    {
        var target = GetTarget(ent.Comp, action.TargetProviderKey);
        if (target == null || !_xformQuery.TryGetComponent(target.Value, out var targetXform))
            return;

        if (!_xformQuery.TryGetComponent(ent, out var npcXform))
            return;

        var npcMapUid = npcXform.MapUid;
        var targetMapUid = targetXform.MapUid;
        if (npcMapUid == null || targetMapUid == null)
            return;

        // Same map — direct steering to target
        if (npcMapUid == targetMapUid)
        {
            _pendingAscent.Remove(ent.Owner, out _);
            _pendingDescent.Remove(ent.Owner, out _);
            var comp = _steering.Register(ent, targetXform.Coordinates);
            comp.Range = action.Range;
            return;
        }

        // Different maps — compute Z-direction
        var zOffset = GetZOffset(npcMapUid.Value, targetMapUid.Value);
        if (zOffset == 0)
            return;

        var npcWorldPos = _transformSys.GetWorldPosition(npcXform);

        if (zOffset > 0)
        {
            RegisterAscent(ent, action, npcMapUid.Value, npcWorldPos);
        }
        else
        {
            RegisterDescent(ent, action, npcMapUid.Value, npcWorldPos);
        }
    }

    /// <summary>
    /// Finds the nearest slope on the current map and steers to its uphill edge.
    /// The cached direction is DOWNHILL (GetCardinalDir convention), so
    /// uphill = slopeDir.GetOpposite().
    /// When the NPC arrives, <see cref="OnActionUpdate"/> teleports it one Z-level up.
    /// </summary>
    private void RegisterAscent(
        Entity<CEGOAPComponent> ent,
        CEGOAPMoveToTargetAction action,
        EntityUid npcMapUid,
        Vector2 npcWorldPos)
    {
        if (!_gridQuery.TryGetComponent(npcMapUid, out var grid))
            return;

        if (!_slopeCache.TryFindNearestSlope(npcMapUid, npcWorldPos, action.SlopeSearchRadius,
                out var slopeTilePos, out var cachedSlope))
            return;

        // cachedSlope.Direction = downhill. Uphill = GetOpposite().
        // Steer to the uphill edge of the slope tile (the border where height reaches 1.0).
        var uphillDir = cachedSlope.Direction.GetOpposite();
        var slopeTileCenter = _mapSystem.GridTileToLocal(npcMapUid, grid, slopeTilePos);
        var edgeOffset = uphillDir.ToVec() * 0.45f;
        var targetCoords = new EntityCoordinates(slopeTileCenter.EntityId,
            slopeTileCenter.Position + edgeOffset);

        var comp = _steering.Register(ent, targetCoords);
        comp.Range = 0.1f;

        _pendingAscent[ent.Owner] = cachedSlope.Direction;
        _pendingDescent.Remove(ent.Owner);
    }

    /// <summary>
    /// Finds the nearest slope on the map below and locates a walkable tile on the current map
    /// near that slope's position. Steers the NPC there, and when they arrive,
    /// <see cref="OnActionUpdate"/> teleports them down via <see cref="CESharedZLevelsSystem.TryMoveDown"/>.
    /// </summary>
    private void RegisterDescent(
        Entity<CEGOAPComponent> ent,
        CEGOAPMoveToTargetAction action,
        EntityUid npcMapUid,
        Vector2 npcWorldPos)
    {
        if (!_gridQuery.TryGetComponent(npcMapUid, out var grid))
            return;

        if (!_zLevels.TryMapDown((npcMapUid, null), out var belowMap))
            return;

        // Find the nearest slope on the map below — world coords are shared across Z-levels.
        if (!_slopeCache.TryFindNearestSlope(belowMap.Value, npcWorldPos, action.SlopeSearchRadius,
                out var slopeTilePos, out var cachedSlope))
            return;

        // cachedSlope.Direction = downhill. Uphill = GetOpposite().
        // The approach tile is one tile in the UPHILL direction from the slope tile.
        // On the upper map, this should be a floor tile (it's where ascending entities arrive).
        var uphillDir = cachedSlope.Direction.GetOpposite();
        var approachTile = slopeTilePos + uphillDir.ToIntVec();

        // If the approach tile has no floor, search nearby for any walkable tile.
        Vector2i targetTile;
        if (HasFloor(npcMapUid, grid, approachTile))
        {
            targetTile = approachTile;
        }
        else if (!TryFindNearestWalkableTile(npcMapUid, grid, slopeTilePos,
                     (int) action.SlopeSearchRadius, out targetTile))
        {
            return;
        }

        // Steer to the edge of the target tile closest to the slope (= downhill edge).
        var tileCenter = _mapSystem.GridTileToLocal(npcMapUid, grid, targetTile);
        var edgeOffset = cachedSlope.Direction.ToVec() * 0.4f;
        var targetCoords = new EntityCoordinates(tileCenter.EntityId,
            tileCenter.Position + edgeOffset);

        var comp = _steering.Register(ent, targetCoords);
        comp.Range = 0.3f;

        _pendingDescent[ent.Owner] = new DescentData(cachedSlope.Direction, belowMap.Value);
        _pendingAscent.Remove(ent.Owner);
    }

    /// <summary>
    /// Finds the nearest tile with floor on a grid around a given tile position.
    /// Searches in expanding rings from the center outward.
    /// </summary>
    private bool TryFindNearestWalkableTile(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i centerTile,
        int maxRange,
        out Vector2i foundTile)
    {
        foundTile = default;

        // Check center tile first
        if (HasFloor(gridUid, grid, centerTile))
        {
            foundTile = centerTile;
            return true;
        }

        // Search expanding rings
        for (var r = 1; r <= maxRange; r++)
        {
            var bestDistSq = float.MaxValue;
            var found = false;

            for (var dx = -r; dx <= r; dx++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    // Only check the ring perimeter
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                        continue;

                    var checkTile = centerTile + new Vector2i(dx, dy);
                    if (!HasFloor(gridUid, grid, checkTile))
                        continue;

                    var distSq = dx * dx + dy * dy;
                    if (distSq >= bestDistSq)
                        continue;

                    bestDistSq = distSq;
                    foundTile = checkTile;
                    found = true;
                }
            }

            if (found)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a tile position on a grid has floor (is not empty).
    /// </summary>
    private bool HasFloor(EntityUid gridUid, MapGridComponent grid, Vector2i tilePos)
    {
        return _mapSystem.TryGetTileRef(gridUid, grid, tilePos, out var tileRef)
               && !tileRef.Tile.IsEmpty;
    }

    /// <summary>
    /// Computes the Z-offset from the NPC's map to the target's map.
    /// Returns positive if target is above, negative if below, 0 if not in the same Z-network.
    /// </summary>
    private int GetZOffset(EntityUid npcMapUid, EntityUid targetMapUid)
    {
        if (!_zMapQuery.TryGetComponent(npcMapUid, out var npcZMap))
            return 0;

        if (!_zMapQuery.TryGetComponent(targetMapUid, out var targetZMap))
            return 0;

        if (!_zLevels.TryZNetwork((npcMapUid, npcZMap), out var npcNetwork))
            return 0;

        if (!_zLevels.TryZNetwork((targetMapUid, targetZMap), out var targetNetwork))
            return 0;

        if (npcNetwork.Value.Owner != targetNetwork.Value.Owner)
            return 0;

        return targetZMap.Depth - npcZMap.Depth;
    }
}
