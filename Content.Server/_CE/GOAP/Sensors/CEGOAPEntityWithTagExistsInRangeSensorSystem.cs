using Content.Shared._CE.GOAP;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if there is a entity with tag in the range.
/// </summary>
public sealed partial class CEGOAPEntityWithTagExistsInRangeSensor : CEGOAPSensorBase<CEGOAPEntityWithTagExistsInRangeSensor>
{
    /// <summary>
    /// A tag that entity must have.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TagPrototype> TagName;

    /// <summary>
    /// Search range.
    /// </summary>
    [DataField(required: true)]
    public float Range = 5f;
}

public sealed partial class CEGOAPEntityWithTagExistsInRangeSensorSystem : CEGOAPSensorSystem<CEGOAPEntityWithTagExistsInRangeSensor>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPEntityWithTagExistsInRangeSensor> args)
    {
        var entities = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, args.Sensor.Range);
        foreach (var entityUid in entities)
        {
            if (_tagSystem.HasTag(entityUid, args.Sensor.TagName))
            {
                SetState(ref args, true);
                return;
            }
        }
        SetState(ref args, false);
    }
}
