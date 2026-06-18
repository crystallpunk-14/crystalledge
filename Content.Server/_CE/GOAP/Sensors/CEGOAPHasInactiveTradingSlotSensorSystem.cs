using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Trading.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP.Sensors;

[RegisterComponent]
public sealed partial class CEGOAPHasInactiveTradingSlotSensorComponent : Component
{
    [DataField]
    public string ConditionKey = "InactiveTradingSlotVisible";

    [DataField]
    public float ScanRadius = 15f;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1f);

    [ViewVariables]
    public TimeSpan NextUpdateTime;
}

public sealed partial class CEGOAPHasInactiveTradingSlotSensorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPHasInactiveTradingSlotSensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CEGOAPHasInactiveTradingSlotSensorComponent, CEGOAPComponent, CEActiveGOAPComponent>();
        while (query.MoveNext(out var uid, out var sensor, out var goap, out _))
        {
            if (curTime < sensor.NextUpdateTime)
                continue;

            sensor.NextUpdateTime = curTime + sensor.UpdateInterval;
            Evaluate(uid, sensor, goap);
        }
    }

    private void OnRefresh(Entity<CEGOAPHasInactiveTradingSlotSensorComponent> ent, ref CEGOAPSensorRefreshEvent args)
    {
        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        Evaluate(ent, ent.Comp, goap);
    }

    private void Evaluate(EntityUid uid, CEGOAPHasInactiveTradingSlotSensorComponent sensor, CEGOAPComponent goap)
    {
        if (!_xformQuery.TryGetComponent(uid, out var xform))
        {
            goap.WorldState[sensor.ConditionKey] = false;
            return;
        }

        var selfPos = _transform.GetWorldPosition(xform);
        var mapId = xform.MapID;
        var radiusSq = sensor.ScanRadius * sensor.ScanRadius;

        var slotQuery = EntityQueryEnumerator<CETradingSlotComponent, TransformComponent>();
        while (slotQuery.MoveNext(out _, out var slot, out var slotXform))
        {
            if (slot.ActiveOffer != null)
                continue;

            if (slot.Offers.Count == 0)
                continue;

            if (slotXform.MapID != mapId)
                continue;

            var slotPos = _transform.GetWorldPosition(slotXform);
            if (Vector2.DistanceSquared(selfPos, slotPos) <= radiusSq)
            {
                goap.WorldState[sensor.ConditionKey] = true;
                return;
            }
        }

        goap.WorldState[sensor.ConditionKey] = false;
    }
}
