using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Navigates to the last-known position of a target.
/// On arrival or if no position is stored, clears LastKnownPositions[key].
/// </summary>
public sealed partial class CEGOAPMoveToLastKnownPositionAction
    : CEGOAPActionBase<CEGOAPMoveToLastKnownPositionAction>
{
    /// <summary>
    /// The target key to look up in LastKnownPositions.
    /// </summary>
    [DataField(required: true)]
    public string PositionTargetKey = string.Empty;

    /// <summary>
    /// How close to get before considering arrival.
    /// </summary>
    [DataField]
    public float Range = 1.5f;
}

public sealed partial class CEGOAPMoveToLastKnownPositionActionSystem
    : CEGOAPActionSystem<CEGOAPMoveToLastKnownPositionAction>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly CEGOAPSystem _goap = default!;

    protected override void OnActionStartup(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionStartupEvent<CEGOAPMoveToLastKnownPositionAction> args)
    {
        if (!ent.Comp.LastKnownPositions.TryGetValue(args.Action.PositionTargetKey, out var coords))
            return;

        var comp = _steering.Register(ent, coords);
        comp.Range = args.Action.Range;
    }

    protected override void OnActionUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionUpdateEvent<CEGOAPMoveToLastKnownPositionAction> args)
    {
        if (!TryComp<NPCSteeringComponent>(ent, out var steering))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        switch (steering.Status)
        {
            case SteeringStatus.InRange:
                _goap.ClearLastKnownPosition(ent, args.Action.PositionTargetKey);
                args.Status = CEGOAPActionStatus.Finished;
                return;
            case SteeringStatus.NoPath:
                _goap.ClearLastKnownPosition(ent, args.Action.PositionTargetKey);
                args.Status = CEGOAPActionStatus.Failed;
                return;
            default:
                args.Status = CEGOAPActionStatus.Running;
                return;
        }
    }

    protected override void OnActionShutdown(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionShutdownEvent<CEGOAPMoveToLastKnownPositionAction> args)
    {
        _steering.Unregister(ent);
    }
}
