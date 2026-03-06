using Content.Shared._CE.GOAP;

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
        SubscribeLocalEvent<CEGOAPComponent, CEGOAPSensorUpdateEvent<T>>(OnSensorUpdate);
    }

    /// <summary>
    /// Called with each sensor update tick (default 0.2s).
    /// The sensor scans information about the world and sets the key to which this sensor is bound to true or false.
    /// The sensor MUST NOT affect the world around it or influence the entity itself in any way,
    /// except by setting GOAP state via <see cref="SetState"/>.
    /// </summary>
    protected abstract void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<T> args);

    /// <summary>
    /// Updates the state of the world known to entity. The key we update is automatically taken from the sensor.
    /// </summary>
    protected void SetState(ref CEGOAPSensorUpdateEvent<T> args, bool newState)
    {
        args.WorldState[args.Sensor.ConditionKey] = newState;
    }
}
