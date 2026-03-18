using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if there is a entity with the component in the range.
/// </summary>
public sealed partial class CEGOAPEntityWithComponentExistsInRangeSensor : CEGOAPSensorBase<CEGOAPEntityWithComponentExistsInRangeSensor>
{

    /// <summary>
    /// A component that entity must have.
    /// </summary>
    [DataField(required: true)]
    public string ComponentName;

    /// <summary>
    /// Search range.
    /// </summary>
    [DataField(required: true)]
    public float Range = 5f;
}

public sealed partial class CEGOAPEntityWithComponentExistsInRangeSensorSystem : CEGOAPSensorSystem<CEGOAPEntityWithComponentExistsInRangeSensor>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPEntityWithComponentExistsInRangeSensor> args)
    {
        if (!Factory.TryGetRegistration(args.Sensor.ComponentName, out var registration))
        {
            SetState(ref args, false);
            return;
        }
        var compType = registration.Type;

        var entities = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, args.Sensor.Range);
        foreach (var entityUid in entities)
        {
            if (EntityManager.HasComponent(entityUid, compType))
            {
                SetState(ref args, true);
                return;
            }
        }
        SetState(ref args, false);
    }
}
