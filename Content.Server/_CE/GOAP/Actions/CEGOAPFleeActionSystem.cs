using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;
using Robust.Shared.Map;

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
}

/// <summary>
/// Handles CEGOAPFleeAction execution.
/// Steers the NPC away from its current target.
/// </summary>
public sealed partial class CEGOAPFleeActionSystem : CEGOAPActionSystem<CEGOAPFleeAction>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
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
            // Target lost, flee succeeded
            args.Status = CEGOAPActionStatus.Finished;
            return;
        }

        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(target, out var targetXform))
        {
            args.Status = CEGOAPActionStatus.Finished;
            return;
        }

        // Update flee direction
        UpdateFleeTarget(ent, args.Action.FleeDistance);

        // Check if steering has no path
        if (TryComp<NPCSteeringComponent>(ent, out var steering) &&
            steering.Status == SteeringStatus.NoPath)
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

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
        var fleeWorldPos = npcWorldPos + dir * fleeDistance;

        // Convert world position to parent-local coordinates
        var invMatrix = _transform.GetInvWorldMatrix(xform.ParentUid);
        var localFleePos = Vector2.Transform(fleeWorldPos, invMatrix);
        var fleeCoords = new EntityCoordinates(xform.ParentUid, localFleePos);

        _steering.Register(ent, fleeCoords);
    }
}
