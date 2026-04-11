using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the current target is within a specified range.
/// </summary>
public sealed partial class CEGOAPRangeToTargetSensor : CEGOAPSensorBase<CEGOAPRangeToTargetSensor>
{
    public override TimeSpan? UpdateInterval => TimeSpan.FromSeconds(0.2);

    /// <summary>
    /// Range threshold in tiles.
    /// </summary>
    [DataField(required: true)]
    public float Range = 1f;
}

public sealed partial class CEGOAPRangeToTargetSensorSystem : CEGOAPSensorSystem<CEGOAPRangeToTargetSensor>
{
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    protected override bool? OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPRangeToTargetSensor> args)
    {
        var target = Goap.GetTarget(ent, args.Sensor.TargetKey);
        if (target == null)
            return false;

        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(target.Value, out var targetXform))
            return false;

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
            return false;

        return distance <= args.Sensor.Range;
    }
}
