using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Bridge sensor: picks the nearest hostile from the GOAP knowledge store and writes it into
/// Targets/LastKnownPositions for the legacy action layer. A "fresh" entry (recently refreshed
/// by a perceptor) is treated as currently visible; older entries fall back to last-known-position.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPNearestHostileSensorComponent : Component
{
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    [DataField(required: true)]
    public string OutputTargetKey = string.Empty;

    /// A knowledge entry counts as currently visible only if last refreshed within this window.
    [DataField]
    public TimeSpan FreshnessWindow = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    [ViewVariables]
    public TimeSpan NextUpdateTime;
}

public sealed class CEGOAPNearestHostileSensorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CEGOAPSystem _goap = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<NpcFactionMemberComponent> _factionQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _factionQuery = GetEntityQuery<NpcFactionMemberComponent>();

        SubscribeLocalEvent<CEGOAPNearestHostileSensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
        SubscribeLocalEvent<CEGOAPNearestHostileSensorComponent, CEGOAPKnowledgeUpdatedEvent>(OnKnowledgeUpdated);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CEGOAPNearestHostileSensorComponent, CEGOAPComponent, CEActiveGOAPComponent>();
        while (query.MoveNext(out var uid, out var sensor, out var goap, out _))
        {
            if (curTime < sensor.NextUpdateTime)
                continue;

            sensor.NextUpdateTime = curTime + sensor.UpdateInterval;
            Evaluate(uid, sensor, goap);
        }
    }

    private void OnRefresh(Entity<CEGOAPNearestHostileSensorComponent> ent, ref CEGOAPSensorRefreshEvent args)
    {
        if (TryComp<CEGOAPComponent>(ent, out var goap))
            Evaluate(ent, ent.Comp, goap);
    }

    private void OnKnowledgeUpdated(Entity<CEGOAPNearestHostileSensorComponent> ent, ref CEGOAPKnowledgeUpdatedEvent args)
    {
        if (TryComp<CEGOAPComponent>(ent, out var goap))
            Evaluate(ent, ent.Comp, goap);
    }

    private void Evaluate(EntityUid uid, CEGOAPNearestHostileSensorComponent sensor, CEGOAPComponent goap)
    {
        Entity<CEGOAPComponent> ent = (uid, goap);

        if (!_xformQuery.TryGetComponent(uid, out var xform)
            || !_factionQuery.TryGetComponent(uid, out var selfFaction))
        {
            _goap.SetTarget(ent, sensor.OutputTargetKey, null);
            goap.WorldState[sensor.ConditionKey] = false;
            return;
        }

        var selfPos = _transform.GetWorldPosition(xform);
        var selfMap = xform.MapUid;
        var now = _timing.CurTime;

        EntityUid? freshBest = null;
        EntityUid? rememberedBest = null;
        var freshDist = float.MaxValue;
        var rememberedDist = float.MaxValue;
        EntityCoordinates? rememberedCoords = null;

        foreach (var (target, entry) in goap.Knowledge)
        {
            if (target == uid)
                continue;

            if (!_xformQuery.TryGetComponent(target, out var targetXform))
                continue;

            if (targetXform.MapUid != selfMap)
                continue;

            if (!_factionQuery.TryGetComponent(target, out var targetFaction))
                continue;

            var hostiles = selfFaction.HostileFactions;
            if (!hostiles.Overlaps(targetFaction.Factions))
                continue;

            if (_faction.IsEntityFriendly((uid, selfFaction), (target, targetFaction)))
                continue;

            var dist = Vector2.Distance(selfPos, _transform.GetWorldPosition(targetXform));
            var isFresh = (now - entry.LastSeenTime) <= sensor.FreshnessWindow;

            if (isFresh && dist < freshDist)
            {
                freshDist = dist;
                freshBest = target;
            }

            if (dist < rememberedDist)
            {
                rememberedDist = dist;
                rememberedBest = target;
                rememberedCoords = entry.LastSeenCoords;
            }
        }

        if (freshBest != null)
        {
            _goap.SetTarget(ent, sensor.OutputTargetKey, freshBest);
            goap.WorldState[sensor.ConditionKey] = true;
            return;
        }

        _goap.SetTarget(ent, sensor.OutputTargetKey, null);
        goap.WorldState[sensor.ConditionKey] = false;

        if (rememberedBest != null && rememberedCoords is { } coords)
            _goap.SetLastKnownPosition(ent, sensor.OutputTargetKey, coords);
    }
}
