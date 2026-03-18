using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the current target is neutralized (critical or dead).
/// </summary>
public sealed partial class CEGOAPTargetIsDeadSensor : CEGOAPSensorBase<CEGOAPTargetIsDeadSensor>;

public sealed partial class CEGOAPTargetIsDeadSensorSystem : CEGOAPSensorSystem<CEGOAPTargetIsDeadSensor>
{
    [Dependency] private readonly CEMobStateSystem _mobState = default!;
    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPTargetIsDeadSensor> args)
    {
        var target = GetTarget(ent.Comp, args.Sensor.TargetProviderKey);
        if (target == null)
        {
            SetState(ref args, false);
            return;
        }

        SetState(ref args, !_mobState.IsAlive(target.Value));
    }
}
