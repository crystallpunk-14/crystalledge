using Content.Shared._CE.GOAP;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Triggers action (Instant, EntityTarget, or WorldTarget).
/// The action type is auto-detected from the components on the action entity.
/// </summary>
public sealed partial class CEGOAPUseAction : CEGOAPActionBase<CEGOAPUseAction>
{
    /// <summary>
    /// Prototype ID of the action entity to use.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId ActionPrototype;
}

public sealed partial class CEGOAPUseActionSystem : CEGOAPActionSystem<CEGOAPUseAction>
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private EntityQuery<EntityTargetActionComponent> _entityTargetQuery;
    private EntityQuery<WorldTargetActionComponent> _worldTargetQuery;

    public override void Initialize()
    {
        base.Initialize();
        _entityTargetQuery = GetEntityQuery<EntityTargetActionComponent>();
        _worldTargetQuery = GetEntityQuery<WorldTargetActionComponent>();
    }

    /// <summary>
    /// During planning: check if the action is on cooldown.
    /// If the action hasn't been granted yet, assume it's usable.
    /// </summary>
    protected override void OnCanExecute(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionCanExecuteEvent<CEGOAPUseAction> args)
    {
        var actionEntity = FindActionEntity(ent, args.Action.ActionPrototype);

        // Not yet granted — assume available
        if (actionEntity == null)
            return;

        if (!TryComp<ActionComponent>(actionEntity.Value, out var actionComp))
        {
            args.CanExecute = false;
            return;
        }

        // On cooldown — can't use
        if (_actions.IsCooldownActive(actionComp))
            args.CanExecute = false;
    }

    protected override void OnActionUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionUpdateEvent<CEGOAPUseAction> args)
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

        // Still on cooldown — fail immediately so the planner can pick alternatives
        if (_actions.IsCooldownActive(actionComp))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // Determine the target entity for EntityTarget / WorldTarget actions
        var target = Goap.GetTarget(ent, args.Action.TargetKey);

        // Set target on the action event based on auto-detected type
        if (_entityTargetQuery.HasComponent(actionEntity.Value) ||
            _worldTargetQuery.HasComponent(actionEntity.Value))
        {
            if (target == null)
            {
                args.Status = CEGOAPActionStatus.Failed;
                return;
            }

            _actions.SetEventTarget(actionEntity.Value, target.Value);
        }

        _actions.PerformAction(ent.Owner, (actionEntity.Value, actionComp), predicted: false);
        args.Status = CEGOAPActionStatus.Finished;
    }

    /// <summary>
    /// Finds an already-granted action entity matching the prototype ID.
    /// Does NOT grant a new action — used during planning feasibility checks.
    /// </summary>
    private EntityUid? FindActionEntity(Entity<CEGOAPComponent> ent, EntProtoId actionProto)
    {
        foreach (var action in _actions.GetActions(ent))
        {
            var meta = MetaData(action);
            if (meta.EntityPrototype?.ID == (string) actionProto)
                return action;
        }

        return null;
    }

    /// <summary>
    /// Finds an existing action or grants a new one if not present.
    /// </summary>
    private EntityUid? FindOrGrantAction(Entity<CEGOAPComponent> ent, EntProtoId actionProto)
    {
        var found = FindActionEntity(ent, actionProto);
        if (found != null)
            return found;

        return _actions.AddAction(ent, actionProto);
    }
}
