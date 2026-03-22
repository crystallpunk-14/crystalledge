using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks whether a last-known position exists for a given target key.
/// Returns true if LastKnownPositions contains the key AND the target is currently lost.
/// Event-driven: reacts to CETargetChangedEvent.
/// </summary>
public sealed partial class CEGOAPHasLastKnownPositionSensor
    : CEGOAPSensorBase<CEGOAPHasLastKnownPositionSensor>
{
    /// <summary>
    /// The target key to check in LastKnownPositions.
    /// </summary>
    [DataField(required: true)]
    public string PositionTargetKey = string.Empty;
}

public sealed partial class CEGOAPHasLastKnownPositionSensorSystem
    : CEGOAPSensorSystem<CEGOAPHasLastKnownPositionSensor>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPComponent, CETargetChangedEvent>(OnTargetChanged);
    }

    private void OnTargetChanged(Entity<CEGOAPComponent> ent, ref CETargetChangedEvent args)
    {
        foreach (var sensor in ent.Comp.Sensors)
        {
            if (sensor is not CEGOAPHasLastKnownPositionSensor lastPosSensor)
                continue;

            if (lastPosSensor.PositionTargetKey != args.TargetKey)
                continue;

            ent.Comp.WorldState[lastPosSensor.ConditionKey] = Evaluate(ent.Comp, lastPosSensor);
        }
    }

    protected override bool OnSensorUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPSensorUpdateEvent<CEGOAPHasLastKnownPositionSensor> args)
    {
        return Evaluate(ent.Comp, args.Sensor);
    }

    private static bool Evaluate(CEGOAPComponent comp, CEGOAPHasLastKnownPositionSensor sensor)
    {
        var key = sensor.PositionTargetKey;

        if (!comp.LastKnownPositions.ContainsKey(key))
            return false;

        // Only true if the target is currently lost.
        if (comp.Targets.TryGetValue(key, out var target) && target != null)
            return false;

        return true;
    }
}
