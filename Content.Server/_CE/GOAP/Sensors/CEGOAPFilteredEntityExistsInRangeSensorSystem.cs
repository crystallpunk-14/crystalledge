using Content.Shared._CE.GOAP;
using Content.Shared.Whitelist;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Filter entities components/tags in range.
/// </summary>
public sealed partial class CEGOAPFilteredEntityExistsInRangeSensor : CEGOAPSensorBase<CEGOAPFilteredEntityExistsInRangeSensor>
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

    /// <summary>
    /// Search range.
    /// </summary>
    [DataField(required: true)]
    public float Range = 5f;

    /// <summary>
    /// Ignore self.
    /// </summary>
    [DataField(required: true)]
    public bool IgnoreSelf = true;
}

public sealed partial class CEGOAPFilteredEntityExistsInRangeSensorSystem : CEGOAPSensorSystem<CEGOAPFilteredEntityExistsInRangeSensor>
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPFilteredEntityExistsInRangeSensor> args)
    {
        var entities = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, args.Sensor.Range);
        foreach (var entityUid in entities)
        {
            if (args.Sensor.IgnoreSelf && entityUid == ent.Owner)
                continue;

            if (_whitelist.CheckBoth(entityUid, args.Sensor.Blacklist, args.Sensor.Whitelist))
            {
                SetState(ref args, true);
                return;
            }
        }
        SetState(ref args, false);
    }
}
