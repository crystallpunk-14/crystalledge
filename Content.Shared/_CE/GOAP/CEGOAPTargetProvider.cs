using Robust.Shared.Map;

namespace Content.Shared._CE.GOAP;

/// <summary>
/// Base class for GOAP target providers that resolve entity or coordinate targets.
/// Target providers are resolved each sensor tick and cached for use by actions and sensors.
/// Unlike sensors, providers store resolved targets (EntityUid, EntityCoordinates) rather than boolean state.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEGOAPTargetProvider
{
    /// <summary>
    /// The resolved entity target. Set by the provider system during resolution.
    /// </summary>
    [ViewVariables]
    public EntityUid? TargetEntity;

    /// <summary>
    /// The resolved coordinate target. Set by the provider system during resolution.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates? TargetCoordinates;

    /// <summary>
    /// Raises the resolve event to find and cache a target.
    /// </summary>
    public abstract void RaiseResolve(EntityUid uid, IEntityManager entMan);
}

/// <summary>
/// Generic base for target providers enabling type-safe event dispatch to EntitySystems.
/// </summary>
public abstract partial class CEGOAPTargetProviderBase<T> : CEGOAPTargetProvider
    where T : CEGOAPTargetProviderBase<T>
{
    public override void RaiseResolve(EntityUid uid, IEntityManager entMan)
    {
        if (this is not T self)
            return;

        var ev = new CEGOAPResolveTargetEvent<T>(self);
        entMan.EventBus.RaiseLocalEvent(uid, ref ev);
        TargetEntity = ev.TargetEntity;
        TargetCoordinates = ev.TargetCoordinates;
    }
}

/// <summary>
/// Raised when a target provider needs to resolve its target.
/// The handler should set TargetEntity and/or TargetCoordinates.
/// </summary>
[ByRefEvent]
public record struct CEGOAPResolveTargetEvent<T>(T Provider) where T : CEGOAPTargetProviderBase<T>
{
    public EntityUid? TargetEntity;
    public EntityCoordinates? TargetCoordinates;
}
