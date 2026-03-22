using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the current target is incapacitated (critical).
/// Event-driven: reacts to CEMobStateChangedEvent via CEGOAPTargetComponent.
/// </summary>
public sealed partial class CEGOAPTargetIsDownSensor : CEGOAPSensorBase<CEGOAPTargetIsDownSensor>
{
}

public sealed partial class CEGOAPTargetIsDownSensorSystem : CEGOAPSensorSystem<CEGOAPTargetIsDownSensor>
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
                if (sensor is not CEGOAPTargetIsDownSensor downSensor)
                    continue;

                if (downSensor.TargetKey == null || !keys.Contains(downSensor.TargetKey))
                    continue;

                goap.WorldState[downSensor.ConditionKey] = !_mobState.IsAlive(ent.Owner);
            }
        }
    }

    protected override bool OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPTargetIsDownSensor> args)
    {
        var target = GetTarget(ent, args.Sensor.TargetKey);
        if (target == null)
            return false;

        return !_mobState.IsAlive(target.Value);
    }
}
