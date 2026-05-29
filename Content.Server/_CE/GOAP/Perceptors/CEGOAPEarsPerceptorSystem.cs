using Content.Server._CE.GOAPAlarm;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using System.Numerics;

namespace Content.Server._CE.GOAP.Perceptors;

/// <summary>
/// Hearing-based perception. Reacts to broadcast <see cref="CEGOAPAlarmEvent"/> and feeds
/// the alarm target into the knowledge store of nearby GOAP entities.
/// Knowledge from sound has a short lifetime — once the threat is out of earshot it fades.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPEarsPerceptorComponent : Component
{
}

public sealed class CEGOAPEarsPerceptorSystem : EntitySystem
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

        var targetCoords = Transform(ev.Target).Coordinates;

        var query = EntityQueryEnumerator<CEGOAPEarsPerceptorComponent, CEGOAPComponent, TransformComponent, CEActiveGOAPComponent>();
        while (query.MoveNext(out var uid, out var ears, out var goap, out var xform, out _))
        {
            if (xform.MapUid is null)
                continue;

            if (_zLevel.TryGetZNetwork(xform.MapUid.Value, out var zNetwork))
            {
                if (zNetwork != alarmZNetwork)
                    continue;
            }
            else if (xform.MapUid != alarmMap)
            {
                continue;
            }

            var worldPos = _transform.GetWorldPosition(xform);
            if (Vector2.Distance(alarmPos, worldPos) > ev.Radius)
                continue;

            _goap.Remember((uid, goap), ev.Target, targetCoords);
        }
    }
}
