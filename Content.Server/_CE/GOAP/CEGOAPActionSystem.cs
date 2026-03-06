using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Base EntitySystem for handling GOAP action events.
/// Concrete action systems inherit from this and implement the action logic.
/// </summary>
/// <typeparam name="T">The concrete GOAP action type.</typeparam>
public abstract partial class CEGOAPActionSystem<T> : EntitySystem where T : CEGOAPActionBase<T>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPActionStartupEvent<T>>(OnActionStartup);
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPActionUpdateEvent<T>>(OnActionUpdate);
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPActionShutdownEvent<T>>(OnActionShutdown);
    }

    protected virtual void OnActionStartup(Entity<CEGOAPComponent> ent, ref CEGOAPActionStartupEvent<T> args)
    {
    }

    protected virtual void OnActionUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPActionUpdateEvent<T> args)
    {
    }

    protected virtual void OnActionShutdown(Entity<CEGOAPComponent> ent, ref CEGOAPActionShutdownEvent<T> args)
    {
    }
}
