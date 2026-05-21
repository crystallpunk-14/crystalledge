using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the current target is within a specified range.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPRangeToTargetSensorComponent : Component
{
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    [DataField(required: true)]
    public string TargetKey = string.Empty;

    /// <summary>
    /// Range threshold in tiles.
    /// </summary>
    [DataField(required: true)]
    public float Range = 1f;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.2);

    [ViewVariables]
    public TimeSpan NextUpdateTime;
}

public sealed class CEGOAPRangeToTargetSensorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CEGOAPSystem _goap = default!;

    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPRangeToTargetSensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CEGOAPRangeToTargetSensorComponent, CEGOAPComponent, CEActiveGOAPComponent>();
        while (query.MoveNext(out var uid, out var sensor, out var goap, out _))
        {
            if (curTime < sensor.NextUpdateTime)
                continue;

            sensor.NextUpdateTime = curTime + sensor.UpdateInterval;
            Evaluate(uid, sensor, goap);
        }
    }

    private void OnRefresh(Entity<CEGOAPRangeToTargetSensorComponent> ent, ref CEGOAPSensorRefreshEvent args)
    {
        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        Evaluate(ent, ent.Comp, goap);
    }

    private void Evaluate(EntityUid uid, CEGOAPRangeToTargetSensorComponent sensor, CEGOAPComponent goap)
    {
        Entity<CEGOAPComponent> ent = (uid, goap);

        var target = _goap.GetTarget(ent, sensor.TargetKey);
        if (target == null)
        {
            goap.WorldState[sensor.ConditionKey] = false;
            return;
        }

        if (!_xformQuery.TryGetComponent(uid, out var xform) ||
            !_xformQuery.TryGetComponent(target.Value, out var targetXform))
        {
            goap.WorldState[sensor.ConditionKey] = false;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            goap.WorldState[sensor.ConditionKey] = false;
            return;
        }

        goap.WorldState[sensor.ConditionKey] = distance <= sensor.Range;
    }
}
