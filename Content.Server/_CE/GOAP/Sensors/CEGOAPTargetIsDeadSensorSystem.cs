using Content.Server._CE.Health;
using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the current target is neutralized (critical or dead).
/// </summary>
public sealed partial class CEGOAPTargetIsDeadSensor : CEGOAPSensorBase<CEGOAPTargetIsDeadSensor>;

public sealed partial class CEGOAPTargetIsDeadSensorSystem : CEGOAPSensorSystem<CEGOAPTargetIsDeadSensor>
{
    [Dependency] private readonly CEHealthSystem _health = default!;
    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPTargetIsDeadSensor> args)
    {
        if (ent.Comp.Target is not { } target)
        {
            SetState(ref args, false);
            return;
        }

        SetState(ref args, !_health.IsAlive(target));
    }
}
