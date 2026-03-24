using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Base EntitySystem for handling GOAP sensor events.
/// Concrete sensor systems inherit from this and implement the evaluation logic.
/// </summary>
public abstract partial class CEGOAPSensorSystem<T> : EntitySystem where T : CEGOAPSensorBase<T>
{
    [Dependency] protected readonly CEGOAPSystem Goap = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPSensorUpdateEvent<T>>(HandleSensorUpdate);
    }

    private void HandleSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<T> args)
    {
        var newState = OnSensorUpdate(ent, ref args);

        if (newState is null)
            return;

        args.WorldState[args.Sensor.ConditionKey] = newState.Value;
    }

    /// <summary>
    /// Evaluate a world-state condition and return true/false or null (dont change).
    /// Called on each poll tick and during forced updates.
    /// Must NOT affect the world or entity — only observe.
    /// </summary>
    protected virtual bool? OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<T> args)
    {
        return null;
    }
}
