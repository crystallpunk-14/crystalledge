using System.Numerics;
using Content.Server._CE.GOAPAlarm;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Reacts to global CEGOAPAlarmEvent — sets the alarm target as a GOAP target on nearby
/// GOAP entities that carry this sensor component.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPAlarmSensorComponent : Component
{
    /// <summary>
    /// Key in CEGOAPComponent.Targets to write the alarm target into.
    /// </summary>
    [DataField(required: true)]
    public string OutputTargetKey = string.Empty;
}

public sealed class CEGOAPAlarmSensorSystem : EntitySystem
{
    [Dependency] private readonly CEGOAPSystem _goap = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPAlarmEvent>(OnAlarm);
    }

    private void OnAlarm(CEGOAPAlarmEvent ev)
    {
        var alarmMap = Transform(ev.Target).MapUid;
        if (alarmMap is null)
            return;

        var alarmPos = _transform.ToWorldPosition(ev.Source);
        _zLevel.TryGetZNetwork(alarmMap.Value, out var alarmZNetwork);

        var query = EntityQueryEnumerator<CEGOAPAlarmSensorComponent, CEGOAPComponent, TransformComponent, CEActiveGOAPComponent>();
        while (query.MoveNext(out var uid, out var sensor, out var goap, out var xform, out _))
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

            _goap.SetTarget((uid, goap), sensor.OutputTargetKey, ev.Target);
        }
    }
}
