using Content.Shared._CE.GOAP;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// GOAP action that triggers a WorldTargetAction (spell) at a world position.
/// By default targets the NPC's own position.
/// </summary>
public sealed partial class CEGOAPUseWorldTargetAction : CEGOAPActionBase<CEGOAPUseWorldTargetAction>
{
    /// <summary>
    /// Prototype ID of the action entity to use (e.g. CEActionSpellAreaHealing).
    /// </summary>
    [DataField(required: true)]
    public EntProtoId ActionPrototype;

    /// <summary>
    /// If true, targets the NPC's own position. Otherwise targets the GOAP target entity's position.
    /// </summary>
    [DataField]
    public bool TargetSelf = true;
}

/// <summary>
/// Handles CEGOAPUseWorldTargetAction execution.
/// Grants the action if needed, sets the world target, then performs it.
/// </summary>
public sealed partial class CEGOAPUseWorldTargetActionSystem : CEGOAPActionSystem<CEGOAPUseWorldTargetAction>
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    protected override void OnActionStartup(Entity<CEGOAPComponent> ent, ref CEGOAPActionStartupEvent<CEGOAPUseWorldTargetAction> args)
    {
    }

    protected override void OnActionUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPActionUpdateEvent<CEGOAPUseWorldTargetAction> args)
    {
        EntityUid target;

        if (args.Action.TargetSelf)
        {
            target = ent.Owner;
        }
        else if (ent.Comp.Target is { } t)
        {
            target = t;
        }
        else
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        var actionEntity = FindOrGrantAction(ent, args.Action.ActionPrototype);

        if (actionEntity == null)
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        if (!TryComp<ActionComponent>(actionEntity.Value, out var actionComp))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // Check cooldown
        if (_actions.IsCooldownActive(actionComp))
        {
            args.Status = CEGOAPActionStatus.Running;
            return;
        }

        // SetEventTarget takes an EntityUid and the WorldTargetAction handler
        // converts it to coordinates via Transform(target).Coordinates
        _actions.SetEventTarget(actionEntity.Value, target);
        _actions.PerformAction(ent.Owner, (actionEntity.Value, actionComp), predicted: false);
        args.Status = CEGOAPActionStatus.Finished;
    }

    private EntityUid? FindOrGrantAction(Entity<CEGOAPComponent> ent, EntProtoId actionProto)
    {
        foreach (var action in _actions.GetActions(ent))
        {
            var meta = MetaData(action);
            if (meta.EntityPrototype?.ID == (string) actionProto)
                return action;
        }

        return _actions.AddAction(ent, actionProto);
    }
}
