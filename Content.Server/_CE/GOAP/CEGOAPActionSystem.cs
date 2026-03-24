using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Base EntitySystem for handling GOAP action events.
/// Concrete action systems inherit from this and implement the action logic.
/// </summary>
public abstract partial class CEGOAPActionSystem<T> : EntitySystem where T : CEGOAPActionBase<T>
{
    [Dependency] protected readonly CEGOAPSystem Goap = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPComponent, CEGOAPActionCanExecuteEvent<T>>(OnCanExecute);
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPActionStartupEvent<T>>(OnActionStartup);
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPActionUpdateEvent<T>>(OnActionUpdate);
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPActionShutdownEvent<T>>(OnActionShutdown);
    }

    /// <summary>
    /// Called during planning to check if this action can be executed.
    /// Override to add feasibility checks (e.g., cooldowns, resource availability).
    /// Set args.CanExecute = false to exclude this action from the current planning cycle.
    /// </summary>
    protected virtual void OnCanExecute(Entity<CEGOAPComponent> ent, ref CEGOAPActionCanExecuteEvent<T> args)
    {
    }

    /// <summary>
    /// Called once when the action has just started to be performed.
    /// </summary>
    protected virtual void OnActionStartup(Entity<CEGOAPComponent> ent, ref CEGOAPActionStartupEvent<T> args)
    {
    }

    /// <summary>
    /// Called each tick while the action is being performed. The action should update the status of the action in args.Status.
    /// </summary>
    protected virtual void OnActionUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPActionUpdateEvent<T> args)
    {
    }

    /// <summary>
    /// Called once when the action has finished performing, regardless of whether it succeeded, failed, or was interrupted.
    /// </summary>
    protected virtual void OnActionShutdown(Entity<CEGOAPComponent> ent, ref CEGOAPActionShutdownEvent<T> args)
    {
    }
}
