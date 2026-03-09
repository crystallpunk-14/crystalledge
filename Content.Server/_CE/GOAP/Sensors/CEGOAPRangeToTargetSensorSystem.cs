using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the current target is within a specified range.
/// </summary>
public sealed partial class CEGOAPRangeToTargetSensor : CEGOAPSensorBase<CEGOAPRangeToTargetSensor>
{
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

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPRangeToTargetSensor> args)
    {
        var target = GetTarget(ent.Comp, args.Sensor.TargetProviderKey);
        if (target == null)
        {
            SetState(ref args, false);
            return;
        }

        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(target.Value, out var targetXform))
        {
            SetState(ref args, false);
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            SetState(ref args, false);
            return;
        }

        SetState(ref args, distance <= args.Sensor.Range);
    }
}
