using System.Numerics;
using Content.Shared._CE.GOAP.Selectors;
using Content.Shared._CE.Trading.Components;

namespace Content.Server._CE.GOAP.Selectors;

/// <summary>
/// Resolves to the nearest trading slot that has no active offer (was bought and is waiting for restock).
/// </summary>
public sealed partial class CEGOAPSelectorNearestInactiveTradingSlot : CEGOAPTargetSelectorBase<CEGOAPSelectorNearestInactiveTradingSlot>
{
}

public sealed partial class CEGOAPSelectorNearestInactiveTradingSlotSystem
    : CEGOAPTargetSelectorSystem<CEGOAPSelectorNearestInactiveTradingSlot>
{
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    protected override void Resolve(ref CEGOAPSelectorResolveEvent<CEGOAPSelectorNearestInactiveTradingSlot> ev)
    {
        if (!_xformQuery.TryGetComponent(ev.Agent, out var agentXform))
            return;

        var agentPos = _transform.GetWorldPosition(agentXform);
        var mapId = agentXform.MapID;

        EntityUid? best = null;
        var bestDistSq = float.MaxValue;

        var query = EntityQueryEnumerator<CETradingSlotComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var slot, out var xform))
        {
            if (slot.ActiveOffer != null)
                continue;

            if (slot.Offers.Count == 0)
                continue;

            if (xform.MapID != mapId)
                continue;

            var slotPos = _transform.GetWorldPosition(xform);
            var distSq = Vector2.DistanceSquared(agentPos, slotPos);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = uid;
            }
        }

        if (best == null || !_xformQuery.TryGetComponent(best.Value, out var bestXform))
            return;

        ev.Entity = best.Value;
        ev.Position = bestXform.Coordinates;
    }
}
