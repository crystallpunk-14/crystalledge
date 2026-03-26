using Content.Shared._CE.GOAP;
using Content.Shared.Whitelist;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Filters target components/tags by whitelist and blacklist
/// </summary>
public sealed partial class CEGOAPFilterTargetSensor : CEGOAPSensorBase<CEGOAPFilterTargetSensor>
{
    /// <summary>
    /// Whitelisted components/tags.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;
    /// <summary>
    /// Blacklisted components/tags
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    public override TimeSpan? UpdateInterval => TimeSpan.FromSeconds(0.1);
}

public sealed partial class CEGOAPFilterTargetSensorSystem : CEGOAPSensorSystem<CEGOAPFilterTargetSensor>
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    protected override bool? OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPFilterTargetSensor> args)
    {
        var target = Goap.GetTarget(ent, args.Sensor.TargetKey);
        if (target == null)
        {
            return false;
        }

        return _whitelist.CheckBoth(target.Value, args.Sensor.Blacklist, args.Sensor.Whitelist);
    }
}
