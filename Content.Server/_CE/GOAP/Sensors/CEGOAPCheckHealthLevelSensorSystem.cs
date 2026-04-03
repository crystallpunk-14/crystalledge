using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;
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
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPComponent, CEDamageChangedEvent>(OnDamageChanged);
    }

    /// <summary>
    /// Event-driven update: fires when damage changes on an entity with GOAP.
    /// </summary>
    private void OnDamageChanged(Entity<CEGOAPComponent> ent, ref CEDamageChangedEvent args)
    {
        var fraction = GetHealthFraction(ent);

        foreach (var sensor in ent.Comp.Sensors)
        {
            if (sensor is not CEGOAPCheckHealthLevelSensor healthSensor)
                continue;

            ent.Comp.WorldState[healthSensor.ConditionKey] = fraction < healthSensor.Threshold;
        }
    }

    protected override bool? OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPCheckHealthLevelSensor> args)
    {
        return GetHealthFraction(ent) < args.Sensor.Threshold;
    }

    /// <summary>
    /// Returns health fraction (0..1). Uses <see cref="CEMobStateComponent"/> thresholds when available,
    /// falls back to <see cref="CEDestructibleComponent.DestroyThreshold"/> for entities without mob state.
    /// </summary>
    private float GetHealthFraction(EntityUid uid)
    {
        if (!TryComp<CEDamageableComponent>(uid, out var damage))
            return 1f;

        // Prefer CEMobStateComponent thresholds (has critical + destroy).
        if (TryComp<CEMobStateComponent>(uid, out var mobState))
            return _mobState.GetHealthFraction(uid, damage, mobState);

        // Fallback: use CEDestructible destroy threshold as max health.
        if (TryComp<CEDestructibleComponent>(uid, out var destructible) && destructible.DestroyThreshold > 0)
            return 1f - (float) damage.TotalDamage / destructible.DestroyThreshold;

        return 1f;
    }
}
