using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// GOAP sensor that checks if the current target is neutralized (critical or dead).
/// Checks both CEHealthComponent and vanilla MobStateComponent.
/// </summary>
public sealed partial class CEGOAPEnemyStateSensor : CEGOAPSensorBase<CEGOAPEnemyStateSensor>
{
    /// <summary>
    /// Which condition key this sensor updates.
    /// </summary>
    [DataField]
    public string ConditionKey = "CEEnemyNeutralized";
}

public sealed partial class CEGOAPEnemyStateSensorSystem : CEGOAPSensorSystem<CEGOAPEnemyStateSensor>
{
    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPEnemyStateSensor> args)
    {
        var conditionKey = args.Sensor.ConditionKey;

        if (ent.Comp.Target is not { } target)
        {
            args.WorldState[conditionKey] = false;
            return;
        }

        // Check CE health system first
        if (TryComp<CEHealthComponent>(target, out var ceHealth))
        {
            args.WorldState[conditionKey] = ceHealth.CurrentState >= CEMobState.Critical;
            return;
        }

        // Fall back to vanilla mob state
        if (TryComp<MobStateComponent>(target, out var mobState))
        {
            args.WorldState[conditionKey] = mobState.CurrentState >= MobState.Critical;
            return;
        }

        args.WorldState[conditionKey] = false;
    }
}
