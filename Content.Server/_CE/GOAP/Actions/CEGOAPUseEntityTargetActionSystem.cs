using Content.Shared._CE.GOAP;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// GOAP action that triggers an EntityTargetAction (spell) targeting the GOAP target entity.
/// The action entity is granted on startup if not already present.
/// </summary>
public sealed partial class CEGOAPUseEntityTargetAction : CEGOAPActionBase<CEGOAPUseEntityTargetAction>
{
    /// <summary>
    /// Prototype ID of the action entity to use (e.g. CEActionSpellDivineShield).
    /// </summary>
    [DataField(required: true)]
    public EntProtoId ActionPrototype;

    /// <summary>
    /// If true, target self instead of the GOAP target entity.
    /// </summary>
    [DataField]
    public bool TargetSelf;
}

/// <summary>
/// Handles CEGOAPUseEntityTargetAction execution.
/// Grants the action if needed, sets the entity target, then performs it.
/// </summary>
public sealed partial class CEGOAPUseEntityTargetActionSystem : CEGOAPActionSystem<CEGOAPUseEntityTargetAction>
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    protected override void OnActionStartup(Entity<CEGOAPComponent> ent, ref CEGOAPActionStartupEvent<CEGOAPUseEntityTargetAction> args)
    {
    }

    protected override void OnActionUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPActionUpdateEvent<CEGOAPUseEntityTargetAction> args)
    {
        var target = args.Action.TargetSelf ? ent.Owner : ent.Comp.Target;

        if (target == null)
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

        // Set the target on the action event
        _actions.SetEventTarget(actionEntity.Value, target.Value);
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
