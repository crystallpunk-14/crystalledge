using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks whether a named target provider has successfully resolved a target.
/// Sets the condition to true if the provider's TargetEntity is not null.
/// </summary>
public sealed partial class CEGOAPTargetExistsSensor : CEGOAPSensorBase<CEGOAPTargetExistsSensor>
{
    public override TimeSpan? UpdateInterval => TimeSpan.FromSeconds(0.2);
}

public sealed partial class CEGOAPTargetExistsSensorSystem : CEGOAPSensorSystem<CEGOAPTargetExistsSensor>
{
    protected override bool OnSensorUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPSensorUpdateEvent<CEGOAPTargetExistsSensor> args)
    {
        var key = args.Sensor.TargetProviderKey;
        if (key == null)
            return false;

        if (!ent.Comp.TargetProviders.TryGetValue(key, out var provider))
            return false;

        return provider.TargetEntity != null;
    }
}
