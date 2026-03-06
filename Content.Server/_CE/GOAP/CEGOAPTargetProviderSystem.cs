using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Base EntitySystem for handling GOAP target provider resolution events.
/// Concrete provider systems inherit from this and implement target resolution logic.
/// </summary>
public abstract partial class CEGOAPTargetProviderSystem<T> : EntitySystem
    where T : CEGOAPTargetProviderBase<T>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPResolveTargetEvent<T>>(OnResolve);
    }

    /// <summary>
    /// Called when the target provider needs to resolve its target.
    /// Set args.TargetEntity and/or args.TargetCoordinates to provide a target.
    /// </summary>
    protected abstract void OnResolve(Entity<CEGOAPComponent> ent, ref CEGOAPResolveTargetEvent<T> args);
}
