using Content.Shared._CE.GOAP;
using Content.Shared.Whitelist;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks for the presence of a specific target in the EntityWhitelist within a certain radius and assigns it to the specified target slot
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
    [DataField]
    public float Range = 5f;

    /// <summary>
    /// Minimum count of entities.
    /// </summary>
    [DataField]
    public int MinCount = 1;

    /// <summary>
    /// Make the found entity the target.
    /// </summary>
    [DataField]
    public string OutputTargetKey = string.Empty;

    public override TimeSpan? UpdateInterval => TimeSpan.FromSeconds(0.2);
}

public sealed partial class CEGOAPFilteredEntityExistsInRangeSensorSystem : CEGOAPSensorSystem<CEGOAPFilteredEntityExistsInRangeSensor>
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    protected override bool? OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPFilteredEntityExistsInRangeSensor> args)
    {
        int count = 0;
        var entities = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, args.Sensor.Range);

        foreach (var entityUid in entities)
        {
            if (entityUid == ent.Owner)
                continue;

            if (_whitelist.CheckBoth(entityUid, args.Sensor.Blacklist, args.Sensor.Whitelist))
                count++;

            if (count >= args.Sensor.MinCount)
            {
                if (args.Sensor.OutputTargetKey != string.Empty)
                    Goap.SetTarget(ent, args.Sensor.OutputTargetKey, entityUid);

                return true;
            }
        }
        return false;
    }
}
