using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks whether a named target provider has successfully resolved a target.
/// Sets the condition to true if the provider's TargetEntity is not null.
/// </summary>
public sealed partial class CEGOAPTargetExistsSensor : CEGOAPSensorBase<CEGOAPTargetExistsSensor>;

public sealed partial class CEGOAPTargetExistsSensorSystem : CEGOAPSensorSystem<CEGOAPTargetExistsSensor>
{
    protected override void OnSensorUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPSensorUpdateEvent<CEGOAPTargetExistsSensor> args)
    {
        var key = args.Sensor.TargetProviderKey;
        if (key == null)
        {
            SetState(ref args, false);
            return;
        }

        if (!ent.Comp.TargetProviders.TryGetValue(key, out var provider))
        {
            SetState(ref args, false);
            return;
        }

        SetState(ref args, provider.TargetEntity != null);
    }
}
