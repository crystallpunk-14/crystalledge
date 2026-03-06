using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// GOAP action that makes the NPC flee away from its current target.
/// </summary>
public sealed partial class CEGOAPFleeAction : CEGOAPActionBase<CEGOAPFleeAction>
{
    /// <summary>
    /// How far ahead to set the flee waypoint (in tiles).
    /// </summary>
    [DataField]
    public float FleeDistance = 15f;

    /// <summary>
    /// How often (in seconds) to recalculate the flee direction.
    /// </summary>
    [DataField]
    public float RecalcInterval = 1f;
}

/// <summary>
/// Handles CEGOAPFleeAction execution.
/// Steers the NPC away from its current target with periodic direction recalculation.
/// </summary>
public sealed partial class CEGOAPFleeActionSystem : CEGOAPActionSystem<CEGOAPFleeAction>
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    protected override void OnActionStartup(Entity<CEGOAPComponent> ent, ref CEGOAPActionStartupEvent<CEGOAPFleeAction> args)
    {
        UpdateFleeTarget(ent, args.Action.FleeDistance);
    }

    protected override void OnActionUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPActionUpdateEvent<CEGOAPFleeAction> args)
    {
        if (ent.Comp.Target is not { } target)
        {
            args.Status = CEGOAPActionStatus.Finished;
            return;
        }

        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(target, out var targetXform))
        {
            args.Status = CEGOAPActionStatus.Finished;
            return;
        }

        if (!TryComp<NPCSteeringComponent>(ent, out var steering))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // Arrived at flee point — finish so planner re-evaluates
        if (steering.Status == SteeringStatus.NoPath)
        {
            // Check if we're close to the target destination (arrived)
            if (xform.Coordinates.TryDistance(EntityManager, steering.Coordinates, out var distToTarget)
                && distToTarget < 2f)
            {
                args.Status = CEGOAPActionStatus.Finished;
                return;
            }

            // Genuinely no path — fail to try replanning
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // Check if flee direction is still valid:
        // If the enemy is now between us and our flee target, we're running toward danger
        var npcPos = _transform.GetWorldPosition(xform);
        var enemyPos = _transform.GetWorldPosition(targetXform);
        var fleeTargetPos = _transform.ToMapCoordinates(steering.Coordinates);

        var dirToFlee = fleeTargetPos.Position - npcPos;
        var dirFromEnemy = npcPos - enemyPos;

        var needsRecalc = false;

        // If flee target is in the enemy's direction (dot < 0), path is invalid
        if (dirToFlee.LengthSquared() > 0.01f && dirFromEnemy.LengthSquared() > 0.01f)
        {
            if (Vector2.Dot(Vector2.Normalize(dirToFlee), Vector2.Normalize(dirFromEnemy)) < 0f)
                needsRecalc = true;
        }

        if (needsRecalc)
            UpdateFleeTarget(ent, args.Action.FleeDistance);

        args.Status = CEGOAPActionStatus.Running;
    }

    protected override void OnActionShutdown(Entity<CEGOAPComponent> ent, ref CEGOAPActionShutdownEvent<CEGOAPFleeAction> args)
    {
        _steering.Unregister(ent);
    }

    private void UpdateFleeTarget(Entity<CEGOAPComponent> ent, float fleeDistance)
    {
        if (ent.Comp.Target is not { } target)
            return;

        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(target, out var targetXform))
            return;

        var npcWorldPos = _transform.GetWorldPosition(xform);
        var targetWorldPos = _transform.GetWorldPosition(targetXform);
        var dir = npcWorldPos - targetWorldPos;

        if (dir.LengthSquared() < 0.01f)
            dir = new Vector2(1, 0);

        dir = Vector2.Normalize(dir);

        // Try progressively shorter distances until we find a valid (non-space) tile
        var mapId = xform.MapID;
        for (var dist = fleeDistance; dist >= 2f; dist -= 2f)
        {
            var fleeWorldPos = npcWorldPos + dir * dist;
            var mapCoords = new MapCoordinates(fleeWorldPos, mapId);

            if (!_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var grid))
                continue;

            var tileIndices = _mapSystem.WorldToTile(gridUid, grid, fleeWorldPos);
            if (!_mapSystem.TryGetTileRef(gridUid, grid, tileIndices, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            // Valid tile found — register as flee destination
            var invMatrix = _transform.GetInvWorldMatrix(xform.ParentUid);
            var localFleePos = Vector2.Transform(fleeWorldPos, invMatrix);
            var fleeCoords = new EntityCoordinates(xform.ParentUid, localFleePos);
            _steering.Register(ent, fleeCoords);
            return;
        }

        // Fallback: use a short distance from current position
        var fallbackPos = npcWorldPos + dir * 2f;
        var fallbackInv = _transform.GetInvWorldMatrix(xform.ParentUid);
        var fallbackLocal = Vector2.Transform(fallbackPos, fallbackInv);
        _steering.Register(ent, new EntityCoordinates(xform.ParentUid, fallbackLocal));
    }
}
