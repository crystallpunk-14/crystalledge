using Content.Shared._CE.GOAP;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Triggers an InstantAction on the NPC.
/// The action entity is granted on startup if not already present.
/// </summary>
public sealed partial class CEGOAPUseInstantAction : CEGOAPActionBase<CEGOAPUseInstantAction>
{
    /// <summary>
    /// Prototype ID of the action entity to use (e.g. CEActionMageIntellectBuff).
    /// </summary>
    [DataField(required: true)]
    public EntProtoId ActionPrototype;
}

public sealed partial class CEGOAPUseInstantActionSystem : CEGOAPActionSystem<CEGOAPUseInstantAction>
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    protected override void OnActionUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPActionUpdateEvent<CEGOAPUseInstantAction> args)
    {
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

        _actions.PerformAction(ent.Owner, (actionEntity.Value, actionComp), predicted: false);
        args.Status = CEGOAPActionStatus.Finished;
    }

    private EntityUid? FindOrGrantAction(Entity<CEGOAPComponent> ent, EntProtoId actionProto)
    {
        // Look for an existing action matching the prototype
        foreach (var action in _actions.GetActions(ent))
        {
            var meta = MetaData(action);
            if (meta.EntityPrototype?.ID == (string) actionProto)
                return action;
        }

        // Grant the action
        return _actions.AddAction(ent, actionProto);
    }
}
