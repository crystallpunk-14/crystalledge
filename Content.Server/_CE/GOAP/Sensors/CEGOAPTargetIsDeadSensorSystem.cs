using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the current target is neutralized (critical or dead).
/// </summary>
public sealed partial class CEGOAPTargetIsDeadSensor : CEGOAPSensorBase<CEGOAPTargetIsDeadSensor>
{
    public override TimeSpan? UpdateInterval => TimeSpan.FromSeconds(0.2);
}

public sealed partial class CEGOAPTargetIsDeadSensorSystem : CEGOAPSensorSystem<CEGOAPTargetIsDeadSensor>
{
    [Dependency] private readonly CEMobStateSystem _mobState = default!;
    protected override bool OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPTargetIsDeadSensor> args)
    {
        var target = GetTarget(ent.Comp, args.Sensor.TargetProviderKey);
        if (target == null)
            return false;

        return !_mobState.IsAlive(target.Value);
    }
}
