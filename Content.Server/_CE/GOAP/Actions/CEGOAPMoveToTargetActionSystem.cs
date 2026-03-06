using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;
using Robust.Shared.Map;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// GOAP action that moves the NPC towards its current target entity.
/// </summary>
public sealed partial class CEGOAPMoveToTargetAction : CEGOAPActionBase<CEGOAPMoveToTargetAction>
{
    /// <summary>
    /// How close the NPC needs to get to the target to consider the action complete.
    /// </summary>
    [DataField]
    public float Range = 1.5f;
}

/// <summary>
/// Handles CEGOAPMoveToTargetAction execution.
/// Steers the NPC towards the current target until within range.
/// </summary>
public sealed partial class CEGOAPMoveToTargetActionSystem : CEGOAPActionSystem<CEGOAPMoveToTargetAction>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    protected override void OnActionStartup(Entity<CEGOAPComponent> ent, ref CEGOAPActionStartupEvent<CEGOAPMoveToTargetAction> args)
    {
        if (ent.Comp.Target is not { } target || !_xformQuery.HasComponent(target))
            return;

        _steering.Register(ent, new EntityCoordinates(target, Vector2.Zero));
    }

    protected override void OnActionUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPActionUpdateEvent<CEGOAPMoveToTargetAction> args)
    {
        if (ent.Comp.Target is not { } target)
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(target, out var targetXform))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // Update steering target in case it moved
        _steering.Register(ent, new EntityCoordinates(target, Vector2.Zero));

        if (distance <= args.Action.Range)
        {
            args.Status = CEGOAPActionStatus.Finished;
            return;
        }

        // Check if steering has no path
        if (TryComp<NPCSteeringComponent>(ent, out var steering) &&
            steering.Status == SteeringStatus.NoPath)
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        args.Status = CEGOAPActionStatus.Running;
    }

    protected override void OnActionShutdown(Entity<CEGOAPComponent> ent, ref CEGOAPActionShutdownEvent<CEGOAPMoveToTargetAction> args)
    {
        _steering.Unregister(ent);
    }
}
