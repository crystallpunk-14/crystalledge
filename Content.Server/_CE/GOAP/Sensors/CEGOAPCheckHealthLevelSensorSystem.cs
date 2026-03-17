using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the entity's own health is below a threshold.
/// Compares accumulated damage against the critical threshold.
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
    private EntityQuery<CEDamageableComponent> _damageQuery;
    private EntityQuery<CEMobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        _damageQuery = GetEntityQuery<CEDamageableComponent>();
        _mobStateQuery = GetEntityQuery<CEMobStateComponent>();
    }

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPCheckHealthLevelSensor> args)
    {
        if (!_damageQuery.TryComp(ent, out var damage) ||
            !_mobStateQuery.TryComp(ent, out var mobState))
        {
            SetState(ref args, false);
            return;
        }

        var healthFraction = mobState.CriticalThreshold > 0
            ? 1f - (float) damage.TotalDamage / mobState.CriticalThreshold
            : 1f;

        SetState(ref args, healthFraction < args.Sensor.Threshold);
    }
}
