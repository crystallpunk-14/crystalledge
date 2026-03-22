using Content.Shared._CE.Health;

namespace Content.Shared._CE.GOAP;

/// <summary>
/// Base class for GOAP sensors that evaluate and update world state conditions.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEGOAPSensor
{
    /// <summary>
    /// The world state key this sensor writes its result to.
    /// </summary>
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    /// <summary>
    /// Optional key into CEGOAPComponent.TargetProviders.
    /// Sensors that need to check something about a specific target reference a provider by this key.
    /// </summary>
    [DataField]
    public string? TargetProviderKey;

    /// <summary>
    /// How often this sensor is polled.
    /// If null or zero, the sensor is purely event-driven and will not be polled.
    /// Defined per concrete sensor class in C#; not serialized.
    /// </summary>
    public virtual TimeSpan? UpdateInterval => null;

    /// <summary>
    /// Next game time at which this sensor should be polled. Runtime state, not serialized.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextUpdateTime;

    /// <summary>
    /// Raises the sensor update event to evaluate world state conditions.
    /// </summary>
    public abstract void RaiseUpdate(EntityUid uid, Dictionary<string, bool> worldState, IEntityManager entMan);
}

/// <summary>
/// Generic base for GOAP sensors enabling type-safe event dispatch to EntitySystems.
/// </summary>
public abstract partial class CEGOAPSensorBase<T> : CEGOAPSensor where T : CEGOAPSensorBase<T>
{
    public override void RaiseUpdate(EntityUid uid, Dictionary<string, bool> worldState, IEntityManager entMan)
    {
        if (this is not T self)
            return;

        var ev = new CEGOAPSensorUpdateEvent<T>(self, worldState);
        entMan.EventBus.RaiseLocalEvent(uid, ref ev);
    }
}

/// <summary>
/// Raised when a sensor needs to evaluate and update the world state.
/// </summary>
[ByRefEvent]
public record struct CEGOAPSensorUpdateEvent<T>(T Sensor, Dictionary<string, bool> WorldState) where T : CEGOAPSensorBase<T>;
