using System.Numerics;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.Sensors;

public sealed partial class CEGOAPAlarmSensor : CEGOAPSensorBase<CEGOAPAlarmSensor>
{
    /// <summary>
    /// Key in CEGOAPComponent.Targets to write the resolved target entity into.
    /// </summary>
    [DataField(required: true)]
    public string OutputTargetKey = string.Empty;
}

public sealed partial class CEGOAPAlarmSensorSystem : CEGOAPSensorSystem<CEGOAPAlarmSensor>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPAlarmEvent>(OnAlarm);
    }

    private void OnAlarm(CEGOAPAlarmEvent ev)
    {
        var alarmXform = Transform(ev.Source);

        var alarmMap = alarmXform.MapUid;
        if (alarmMap is null)
            return;

        var alarmPos = _transform.GetWorldPosition(alarmXform);
        _zLevel.TryGetZNetwork(alarmMap.Value, out var alarmZNetwork);

        var query = EntityQueryEnumerator<CEGOAPComponent, TransformComponent, CEActiveGOAPComponent>();
        while (query.MoveNext(out var uid, out var goap, out var xform, out _))
        {
            if (xform.MapUid is null)
                continue;

            if (_zLevel.TryGetZNetwork(xform.MapUid.Value, out var zNetwork))
            {
                if (zNetwork != alarmZNetwork)
                    continue;
            }
            else
            {
                if (xform.MapUid != alarmMap)
                    continue;
            }

            var worldPos = _transform.GetWorldPosition(xform);

            var distance = Vector2.Distance(alarmPos, worldPos);

            if (distance > ev.Radius)
                continue;

            foreach (var sensor in goap.Sensors)
            {
                if (sensor is not CEGOAPAlarmSensor alarmSensor)
                    continue;
                Goap.SetTarget((uid, goap), alarmSensor.OutputTargetKey, ev.Source);
            }
        }
    }
}

public sealed class CEGOAPAlarmEvent(EntityUid source, float radius) : EntityEventArgs
{
    public EntityUid Source = source;
    public float Radius = radius;
}
