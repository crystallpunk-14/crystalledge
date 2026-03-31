using Content.Shared._CE.GOAP;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;
/// <summary>
/// Checks if there is a prototype in the range.
/// </summary>
public sealed partial class CEGOAPPrototypeExistsInRangeSensor : CEGOAPSensorBase<CEGOAPPrototypeExistsInRangeSensor>
{
    /// <summary>
    /// The prototype ID to find in range.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId PrototypeId;

    /// <summary>
    /// Search range.
    /// </summary>
    [DataField]
    public float Range = 5f;

    /// <summary>
    /// Minimum count of prototypes.
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

public sealed partial class CEGOAPPrototypeExistsInRangeSensorSystem : CEGOAPSensorSystem<CEGOAPPrototypeExistsInRangeSensor>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    protected override bool? OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPPrototypeExistsInRangeSensor> args)
    {
        int count = 0;

        var entities = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, args.Sensor.Range);
        foreach (var entityUid in entities)
        {
            if (entityUid == ent.Owner)
                continue;

            var meta = MetaData(entityUid).EntityPrototype;
            if (meta != null && meta.ID == args.Sensor.PrototypeId)
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
