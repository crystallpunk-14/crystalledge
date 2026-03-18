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
    public EntityPrototype PrototypeId;

    /// <summary>
    /// Search range.
    /// </summary>
    [DataField(required: true)]
    public float Range = 5f;
}

public sealed partial class CEGOAPPrototypeExistsInRangeSensorSystem : CEGOAPSensorSystem<CEGOAPPrototypeExistsInRangeSensor>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPPrototypeExistsInRangeSensor> args)
    {
        var entities = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, args.Sensor.Range);
        foreach (var entityUid in entities)
        {
            if (TryComp<MetaDataComponent>(entityUid, out var meta) &&
                meta.EntityPrototype?.ID == args.Sensor.PrototypeId.ID)
            {
                SetState(ref args, true);
                return;
            }
        }
        SetState(ref args, false);
    }
}
