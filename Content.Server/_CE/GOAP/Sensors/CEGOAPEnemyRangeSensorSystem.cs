using Content.Shared._CE.GOAP;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// GOAP sensor that checks if the current target is within a specified range.
/// </summary>
public sealed partial class CEGOAPEnemyRangeSensor : CEGOAPSensorBase<CEGOAPEnemyRangeSensor>
{
    /// <summary>
    /// Range threshold in tiles.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Which condition key this sensor updates.
    /// </summary>
    [DataField]
    public ProtoId<CEGOAPConditionPrototype> ConditionKey = "CEEnemyInMeleeRange";
}

public sealed partial class CEGOAPEnemyRangeSensorSystem : CEGOAPSensorSystem<CEGOAPEnemyRangeSensor>
{
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPEnemyRangeSensor> args)
    {
        var conditionKey = (string) args.Sensor.ConditionKey;

        if (ent.Comp.Target is not { } target)
        {
            args.WorldState[conditionKey] = false;
            return;
        }

        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(target, out var targetXform))
        {
            args.WorldState[conditionKey] = false;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            args.WorldState[conditionKey] = false;
            return;
        }

        args.WorldState[conditionKey] = distance <= args.Sensor.Range;
    }
}
