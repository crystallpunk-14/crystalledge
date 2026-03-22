using Content.Shared._CE.GOAP;
using Robust.Shared.Map;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Base EntitySystem for handling GOAP sensor events.
/// Concrete sensor systems inherit from this and implement the evaluation logic.
/// </summary>
public abstract partial class CEGOAPSensorSystem<T> : EntitySystem where T : CEGOAPSensorBase<T>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPSensorUpdateEvent<T>>(HandleSensorUpdate);
    }

    private void HandleSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<T> args)
    {
        args.WorldState[args.Sensor.ConditionKey] = OnSensorUpdate(ent, ref args);
    }

    /// <summary>
    /// Evaluate a world-state condition and return true/false.
    /// Called on each poll tick and during forced updates.
    /// Must NOT affect the world or entity — only observe.
    /// </summary>
    protected abstract bool OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<T> args);

    /// <summary>
    /// Returns the resolved entity target from the named target provider, or null if the key is absent or unresolved.
    /// </summary>
    protected EntityUid? GetTarget(CEGOAPComponent goap, string? providerKey)
    {
        if (providerKey == null)
            return null;

        return goap.TargetProviders.TryGetValue(providerKey, out var provider) ? provider.TargetEntity : null;
    }

    /// <summary>
    /// Returns the resolved coordinate target from the named target provider, or null if the key is absent or unresolved.
    /// </summary>
    protected EntityCoordinates? GetTargetCoordinates(CEGOAPComponent goap, string? providerKey)
    {
        if (providerKey == null)
            return null;

        return goap.TargetProviders.TryGetValue(providerKey, out var provider) ? provider.TargetCoordinates : null;
    }
}
