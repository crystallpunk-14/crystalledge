using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the Target has a specific Component.
/// </summary>
public sealed partial class CEGOAPTargetHasComponentSensor : CEGOAPSensorBase<CEGOAPTargetHasComponentSensor>
{
    /// <summary>
    /// A component that Target must have.
    /// </summary>
    [DataField(required: true)]
    public string ComponentName;
}

public sealed partial class CEGOAPTargetHasComponentSensorSystem : CEGOAPSensorSystem<CEGOAPTargetHasComponentSensor>
{

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPTargetHasComponentSensor> args)
    {
        var target = GetTarget(ent.Comp, args.Sensor.TargetProviderKey);
        if (target == null || !Factory.TryGetRegistration(args.Sensor.ComponentName, out var registration))
        {
            SetState(ref args, false);
            return;
        }

        var compType = registration.Type;

        SetState(ref args, EntityManager.HasComponent(target.Value, compType));
    }
}
