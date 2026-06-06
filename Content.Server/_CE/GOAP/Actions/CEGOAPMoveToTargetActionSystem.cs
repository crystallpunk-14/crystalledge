using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Moves the NPC towards its current target entity.
/// Pathfinding (including across Z-levels via ramp portals) and the vertical physics that crosses
/// levels are handled by the steering/pathfinding layers; this action is map-agnostic.
/// </summary>
public sealed partial class CEGOAPMoveToTargetAction : CEGOAPActionBase<CEGOAPMoveToTargetAction>
{
    /// <summary>
    /// How close the NPC needs to get to the target to consider the action complete.
    /// </summary>
    [DataField]
    public float Range = 1f;

    /// <summary>
    /// How far the target must move before re-registering the steering destination.
    /// Prevents constant pathfinding recalculation while still tracking moving targets.
    /// </summary>
    [DataField]
    public float ReregisterThreshold = 1f;
}

public sealed partial class CEGOAPMoveToTargetActionSystem : CEGOAPActionSystem<CEGOAPMoveToTargetAction>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private readonly EntityQuery<NPCSteeringComponent> _steeringQuery = default!;

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
        if (!TryResolveCoords(ent, args.Action.Selector, out var coords))
            return;

        if (!_xformQuery.TryGetComponent(ent, out var npcXform))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        var sameMapAsTarget = npcXform.MapUid == _transform.GetMap(coords);

        if (_steeringQuery.TryComp(ent, out var steering))
        {
            // Re-register if the target moved significantly.
            if (steering.Coordinates.TryDistance(EntityManager, coords, out var delta) &&
                delta > args.Action.ReregisterThreshold)
            {
                var comp = _steering.Register(ent, coords);
                comp.Range = args.Action.Range;
            }

            switch (steering.Status)
            {
                case SteeringStatus.InRange:
                    // Only finished once we're actually on the target's map.
                    if (sameMapAsTarget)
                    {
                        args.Status = CEGOAPActionStatus.Finished;
                        return;
                    }

                    // In range of a path node but not yet on the target map: keep going.
                    RegisterSteering(ent, args.Action);
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
        _steering.Unregister(ent);
    }

    private void RegisterSteering(Entity<CEGOAPComponent> ent, CEGOAPMoveToTargetAction action)
    {
        if (!TryResolveCoords(ent, action.Selector, out var coords))
            return;

        if (!_xformQuery.TryGetComponent(ent, out _))
            return;

        var comp = _steering.Register(ent, coords);
        comp.Range = action.Range;
    }
}
