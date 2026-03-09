namespace Content.Shared._CE.GOAP;

/// <summary>
/// Execution status of a GOAP action.
/// </summary>
public enum CEGOAPActionStatus : byte
{
    /// <summary>
    /// Nothing happens, we continue to perform the current action
    /// </summary>
    Running,
    /// <summary>
    /// Starts the next action in the plan
    /// </summary>
    Finished,
    /// <summary>
    /// Immediately triggers re-planning.
    /// </summary>
    Failed,
}

/// <summary>
/// Base class for all GOAP actions. Defines preconditions, effects, and cost for planning,
/// and dispatches typed events for execution via EntitySystems.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEGOAPAction
{
    /// <summary>
    /// World state conditions required before this action can execute.
    /// </summary>
    [DataField]
    public Dictionary<string, bool> Preconditions = new();

    /// <summary>
    /// World state changes produced when this action completes successfully.
    /// </summary>
    [DataField]
    public Dictionary<string, bool> Effects = new();

    /// <summary>
    /// Cost of performing this action. Lower cost actions are preferred by the planner.
    /// </summary>
    [DataField]
    public float Cost = 1f;

    /// <summary>
    /// Key into the CEGOAPComponent.TargetProviders dictionary.
    /// Actions that need a target read from this provider.
    /// </summary>
    [DataField]
    public string? TargetProviderKey;

    /// <summary>
    /// Checks whether this action can currently be executed (e.g. not on cooldown).
    /// Called by the planner to filter unavailable actions before planning.
    /// </summary>
    public abstract bool RaiseCanExecute(EntityUid uid, IEntityManager entMan);

    /// <summary>
    /// Raises the startup event on the entity to begin action execution.
    /// </summary>
    public abstract void RaiseStartup(EntityUid uid, IEntityManager entMan);

    /// <summary>
    /// Raises the update event on the entity and returns execution status.
    /// </summary>
    public abstract CEGOAPActionStatus RaiseUpdate(EntityUid uid, float frameTime, IEntityManager entMan);

    /// <summary>
    /// Raises the shutdown event on the entity to clean up action state.
    /// </summary>
    public abstract void RaiseShutdown(EntityUid uid, IEntityManager entMan);
}

/// <summary>
/// Generic base for GOAP actions enabling type-safe event dispatch to EntitySystems.
/// </summary>
public abstract partial class CEGOAPActionBase<T> : CEGOAPAction where T : CEGOAPActionBase<T>
{
    public override bool RaiseCanExecute(EntityUid uid, IEntityManager entMan)
    {
        if (this is not T self)
            return false;

        var ev = new CEGOAPActionCanExecuteEvent<T>(self);
        entMan.EventBus.RaiseLocalEvent(uid, ref ev);
        return ev.CanExecute;
    }

    public override void RaiseStartup(EntityUid uid, IEntityManager entMan)
    {
        if (this is not T self)
            return;

        var ev = new CEGOAPActionStartupEvent<T>(self);
        entMan.EventBus.RaiseLocalEvent(uid, ref ev);
    }

    public override CEGOAPActionStatus RaiseUpdate(EntityUid uid, float frameTime, IEntityManager entMan)
    {
        if (this is not T self)
            return CEGOAPActionStatus.Failed;

        var ev = new CEGOAPActionUpdateEvent<T>(self, frameTime);
        entMan.EventBus.RaiseLocalEvent(uid, ref ev);
        return ev.Status;
    }

    public override void RaiseShutdown(EntityUid uid, IEntityManager entMan)
    {
        if (this is not T self)
            return;

        var ev = new CEGOAPActionShutdownEvent<T>(self);
        entMan.EventBus.RaiseLocalEvent(uid, ref ev);
    }
}

/// <summary>
/// Raised when a GOAP action begins execution.
/// </summary>
[ByRefEvent]
public record struct CEGOAPActionStartupEvent<T>(T Action) where T : CEGOAPActionBase<T>;

/// <summary>
/// Raised each frame during GOAP action execution. Set Status to indicate result.
/// </summary>
[ByRefEvent]
public record struct CEGOAPActionUpdateEvent<T>(T Action, float FrameTime) where T : CEGOAPActionBase<T>
{
    public CEGOAPActionStatus Status = CEGOAPActionStatus.Running;
}

/// <summary>
/// Raised when a GOAP action stops execution (on completion, failure, or plan change).
/// </summary>
[ByRefEvent]
public record struct CEGOAPActionShutdownEvent<T>(T Action) where T : CEGOAPActionBase<T>;

/// <summary>
/// Raised during planning to check if this action can currently be executed.
/// Set CanExecute to false to exclude the action from the current plan.
/// </summary>
[ByRefEvent]
public record struct CEGOAPActionCanExecuteEvent<T>(T Action) where T : CEGOAPActionBase<T>
{
    public bool CanExecute = true;
}
