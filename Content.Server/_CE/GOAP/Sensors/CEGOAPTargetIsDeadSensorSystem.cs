using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the current target is neutralized (critical or dead).
/// Event-driven: reacts to CEMobStateChangedEvent via CEGOAPTargetComponent.
/// </summary>
public sealed partial class CEGOAPTargetIsDeadSensor : CEGOAPSensorBase<CEGOAPTargetIsDeadSensor>
{
}

public sealed partial class CEGOAPTargetIsDeadSensorSystem : CEGOAPSensorSystem<CEGOAPTargetIsDeadSensor>
{
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPTargetComponent, CEMobStateChangedEvent>(OnTargetMobStateChanged);
    }

    private void OnTargetMobStateChanged(Entity<CEGOAPTargetComponent> ent, ref CEMobStateChangedEvent args)
    {
        foreach (var (goapUid, keys) in ent.Comp.Trackers)
        {
            if (!TryComp<CEGOAPComponent>(goapUid, out var goap))
                continue;

            foreach (var sensor in goap.Sensors)
            {
                if (sensor is not CEGOAPTargetIsDeadSensor deadSensor)
                    continue;

                if (deadSensor.TargetKey == null || !keys.Contains(deadSensor.TargetKey))
                    continue;

                goap.WorldState[deadSensor.ConditionKey] = !_mobState.IsAlive(ent.Owner);
            }
        }
    }

    protected override bool OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPTargetIsDeadSensor> args)
    {
        var target = GetTarget(ent, args.Sensor.TargetKey);
        if (target == null)
            return false;

        return !_mobState.IsAlive(target.Value);
    }
}
