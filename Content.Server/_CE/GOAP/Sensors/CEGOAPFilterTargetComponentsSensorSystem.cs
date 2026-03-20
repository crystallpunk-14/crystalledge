using Content.Shared._CE.GOAP;
using Content.Shared.Whitelist;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Filters target components/tags by whitelist and blacklist
/// </summary>
public sealed partial class CEGOAPFilterTargetComponentsSensor : CEGOAPSensorBase<CEGOAPFilterTargetComponentsSensor>
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
}

public sealed partial class CEGOAPFilterTargetComponentsSensorSystem : CEGOAPSensorSystem<CEGOAPFilterTargetComponentsSensor>
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPFilterTargetComponentsSensor> args)
    {
        var target = GetTarget(ent.Comp, args.Sensor.TargetProviderKey);
        if (target == null)
        {
            SetState(ref args, false);
            return;
        }

        SetState(ref args, _whitelist.CheckBoth(target.Value, args.Sensor.Blacklist, args.Sensor.Whitelist));
    }
}
