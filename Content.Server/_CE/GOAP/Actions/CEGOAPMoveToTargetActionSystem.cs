using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Moves the NPC towards its current target entity.
/// Uses absolute grid coordinates for proper pathfinding (avoiding space tiles).
/// Only re-registers steering when the target moves significantly.
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
}

public sealed partial class CEGOAPMoveToTargetActionSystem : CEGOAPActionSystem<CEGOAPMoveToTargetAction>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<NPCSteeringComponent> _steeringQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _steeringQuery = GetEntityQuery<NPCSteeringComponent>();
    }

    protected override void OnActionStartup(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionStartupEvent<CEGOAPMoveToTargetAction> args)
    {
        var target = GetTarget(ent, args.Action.TargetKey);
        if (target == null || !_xformQuery.TryGetComponent(target.Value, out var targetXform))
            return;

        var comp = _steering.Register(ent, targetXform.Coordinates);
        comp.Range = args.Action.Range;
    }

    protected override void OnActionUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionUpdateEvent<CEGOAPMoveToTargetAction> args)
    {
        var target = GetTarget(ent, args.Action.TargetKey);
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

        // Re-register steering if target has moved significantly
        if (_steeringQuery.TryComp(ent, out var steering))
        {
            if (steering.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var delta)
                && delta > args.Action.ReregisterThreshold)
            {
                var comp = _steering.Register(ent, targetXform.Coordinates);
                comp.Range = args.Action.Range;
            }

            switch (steering.Status)
            {
                case SteeringStatus.InRange:
                    args.Status = CEGOAPActionStatus.Finished;
                    return;
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
        _steering.Unregister(ent);
    }
}
