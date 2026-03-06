using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the entity's own health is below a threshold.
/// Uses CEHealthComponent for health evaluation.
/// </summary>
public sealed partial class CEGOAPCheckHealthLevelSensor : CEGOAPSensorBase<CEGOAPCheckHealthLevelSensor>
{
    /// <summary>
    /// Health fraction (0..1) below which the condition is set to true.
    /// </summary>
    [DataField]
    public float Threshold = 0.5f;
}

public sealed partial class CEGOAPCheckHealthLevelSensorSystem : CEGOAPSensorSystem<CEGOAPCheckHealthLevelSensor>
{
    private EntityQuery<CEHealthComponent> _healthQuery;

    public override void Initialize()
    {
        base.Initialize();

        _healthQuery = GetEntityQuery<CEHealthComponent>();
    }

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPCheckHealthLevelSensor> args)
    {
        if (!_healthQuery.TryComp(ent, out var health))
        {
            SetState(ref args, false);
            return;
        }

        var healthFraction = health.MaxHealth > 0
            ? (float) health.Health / health.MaxHealth
            : 1f;

        SetState(ref args, healthFraction < args.Sensor.Threshold);
    }
}
