using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// GOAP sensor that checks if the entity's own health is below a threshold.
/// Uses CEHealthComponent for health evaluation.
/// </summary>
public sealed partial class CEGOAPHealthSensor : CEGOAPSensorBase<CEGOAPHealthSensor>
{
    /// <summary>
    /// Health fraction (0..1) below which the condition is set to true.
    /// </summary>
    [DataField]
    public float Threshold = 0.5f;

    /// <summary>
    /// Which condition key this sensor updates.
    /// </summary>
    [DataField]
    public ProtoId<CEGOAPConditionPrototype> ConditionKey = "CELowHealth";
}

public sealed partial class CEGOAPHealthSensorSystem : CEGOAPSensorSystem<CEGOAPHealthSensor>
{
    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPHealthSensor> args)
    {
        var conditionKey = (string) args.Sensor.ConditionKey;

        if (!TryComp<CEHealthComponent>(ent, out var health))
        {
            args.WorldState[conditionKey] = false;
            return;
        }

        var healthFraction = health.MaxHealth > 0
            ? (float) health.Health / health.MaxHealth
            : 1f;

        args.WorldState[conditionKey] = healthFraction < args.Sensor.Threshold;
    }
}
