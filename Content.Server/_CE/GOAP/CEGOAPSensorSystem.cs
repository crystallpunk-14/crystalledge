using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Base EntitySystem for handling GOAP sensor events.
/// Concrete sensor systems inherit from this and implement the evaluation logic.
/// </summary>
public abstract partial class CEGOAPSensorSystem<T> : EntitySystem where T : CEGOAPSensorBase<T>
{
    [Dependency] private readonly CEGOAPSystem _goap = default!;

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
    /// Returns the entity target from the Targets dictionary.
    /// "self" returns the owner, null returns null.
    /// </summary>
    protected EntityUid? GetTarget(Entity<CEGOAPComponent> ent, string? targetKey)
        => _goap.GetTarget(ent, targetKey);

    /// <summary>
    /// Writes a target entity into the component’s Targets dictionary
    /// and automatically tracks its last-known position.
    /// </summary>
    protected void SetTarget(Entity<CEGOAPComponent> ent, string key, EntityUid? target)
        => _goap.SetTarget(ent, key, target);
}
