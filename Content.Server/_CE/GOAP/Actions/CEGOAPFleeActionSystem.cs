using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Flee away from its current target using pathfinding.
/// Samples candidate positions and scores them by distance from threat and
/// whether they are hidden from the threat's line-of-sight.
/// </summary>
public sealed partial class CEGOAPFleeAction : CEGOAPActionBase<CEGOAPFleeAction>
{
    /// <summary>
    /// How far to search for flee destinations (in tiles).
    /// </summary>
    [DataField]
    public float FleeDistance = 10f;

    /// <summary>
    /// Threat's vision radius. Positions beyond this or behind cover are considered hidden.
    /// </summary>
    [DataField]
    public float VisionRadius = 10f;

    /// <summary>
    /// Bonus score for positions not visible to the threat.
    /// A value of 2.0 means an occluded tile is worth 2x the maximum distance score.
    /// </summary>
    [DataField]
    public float OcclusionBonus = 2f;

    /// <summary>
    /// Number of directions to sample when searching for flee positions.
    /// </summary>
    [DataField]
    public int SampleDirections = 12;
}

public sealed partial class CEGOAPFleeActionSystem : CEGOAPActionSystem<CEGOAPFleeAction>
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    protected override void OnActionStartup(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionStartupEvent<CEGOAPFleeAction> args)
    {
        var target = GetTarget(ent, args.Action.TargetKey);
        if (target == null)
            return;

        FindAndRegisterFleeTarget(ent, target.Value, args.Action);
    }

    protected override void OnActionUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionUpdateEvent<CEGOAPFleeAction> args)
    {
        var target = GetTarget(ent, args.Action.TargetKey);
        if (target == null)
        {
            args.Status = CEGOAPActionStatus.Finished;
            return;
        }

        if (!TryComp<NPCSteeringComponent>(ent, out var steering))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        switch (steering.Status)
        {
            case SteeringStatus.InRange:
                args.Status = CEGOAPActionStatus.Finished;
                return;
            case SteeringStatus.NoPath:
                args.Status = CEGOAPActionStatus.Failed;
                return;
            default:
                args.Status = CEGOAPActionStatus.Running;
                return;
        }
    }

    protected override void OnActionShutdown(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionShutdownEvent<CEGOAPFleeAction> args)
    {
        _steering.Unregister(ent);
    }

    /// <summary>
    /// Samples candidate positions around the NPC and picks the best one
    /// based on distance from the threat and whether the position is hidden.
    /// </summary>
    private void FindAndRegisterFleeTarget(
        Entity<CEGOAPComponent> ent,
        EntityUid threat,
        CEGOAPFleeAction action)
    {
        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(threat, out var threatXform))
            return;

        var npcWorldPos = _transform.GetWorldPosition(xform);
        var threatWorldPos = _transform.GetWorldPosition(threatXform);
        var mapId = xform.MapID;

        var bestScore = float.MinValue;
        Vector2? bestWorldPos = null;

        var angleStep = MathF.PI * 2f / action.SampleDirections;

        // Sample at two distances: full and half
        float[] distances = [action.FleeDistance, action.FleeDistance * 0.5f];

        foreach (var dist in distances)
        {
            for (var i = 0; i < action.SampleDirections; i++)
            {
                var angle = angleStep * i;
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var candidatePos = npcWorldPos + dir * dist;

                // Check if on a valid grid tile
                var mapCoords = new MapCoordinates(candidatePos, mapId);
                if (!_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var grid))
                    continue;

                var tileIndices = _mapSystem.WorldToTile(gridUid, grid, candidatePos);
                if (!_mapSystem.TryGetTileRef(gridUid, grid, tileIndices, out var tileRef) ||
                    tileRef.Tile.IsEmpty)
                    continue;

                // Score: distance from threat (normalized)
                var distFromThreat = Vector2.Distance(candidatePos, threatWorldPos);
                var distScore = distFromThreat / action.FleeDistance;

                // Score: visibility — is this position hidden from the threat?
                float visScore = 0f;
                if (distFromThreat > action.VisionRadius)
                {
                    // Beyond threat's vision range
                    visScore = 1f;
                }
                else
                {
                    // Check occlusion (is there a wall between threat and candidate?)
                    // InRangeUnOccluded returns true if visible, so we want it to be false
                    if (!_examine.InRangeUnOccluded(
                            threat,
                            ent,
                            action.VisionRadius + 0.5f))
                    {
                        visScore = 1f;
                    }
                }

                var totalScore = distScore + visScore * action.OcclusionBonus;
                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    bestWorldPos = candidatePos;
                }
            }
        }

        if (bestWorldPos == null)
        {
            // Fallback: flee directly away from the threat at short distance
            var awayDir = npcWorldPos - threatWorldPos;
            if (awayDir.LengthSquared() < 0.01f)
                awayDir = new Vector2(1, 0);
            awayDir = Vector2.Normalize(awayDir);
            bestWorldPos = npcWorldPos + awayDir * 2f;
        }

        // Convert to local coordinates and register with steering
        var invMatrix = _transform.GetInvWorldMatrix(xform.ParentUid);
        var localPos = Vector2.Transform(bestWorldPos.Value, invMatrix);
        var fleeCoords = new EntityCoordinates(xform.ParentUid, localPos);
        var comp = _steering.Register(ent, fleeCoords);
        comp.Range = 1.5f;
    }
}
