using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Examine;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Finds the nearest hostile entity within vision range with line-of-sight check.
/// Sets the condition to true if a hostile is found and writes it to Targets[OutputTargetKey].
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPNearestHostileSensorComponent : Component
{
    /// <summary>
    /// World state key this sensor writes its result to.
    /// </summary>
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    /// <summary>
    /// Key in CEGOAPComponent.Targets to write the resolved target entity into.
    /// </summary>
    [DataField(required: true)]
    public string OutputTargetKey = string.Empty;

    /// <summary>
    /// Detection range in tiles.
    /// </summary>
    [DataField]
    public float VisionRadius = 10f;

    /// <summary>
    /// How often this sensor is polled.
    /// </summary>
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
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPNearestHostileSensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
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
        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        Evaluate(ent, ent.Comp, goap);
    }

    private void Evaluate(EntityUid uid, CEGOAPNearestHostileSensorComponent sensor, CEGOAPComponent goap)
    {
        Entity<CEGOAPComponent> ent = (uid, goap);

        if (!_xformQuery.TryGetComponent(uid, out var xform))
        {
            _goap.SetTarget(ent, sensor.OutputTargetKey, null);
            goap.WorldState[sensor.ConditionKey] = false;
            return;
        }

        var npcWorldPos = _transform.GetWorldPosition(xform);
        Entity<NpcFactionMemberComponent?, FactionExceptionComponent?> factionEnt = (uid, null, null);
        var hostiles = _faction.GetNearbyHostiles(factionEnt, sensor.VisionRadius);

        EntityUid? closestTarget = null;
        var closestDistance = float.MaxValue;

        foreach (var targetUid in hostiles)
        {
            if (!_xformQuery.TryGetComponent(targetUid, out var targetXform))
                continue;

            var targetWorldPos = _transform.GetWorldPosition(targetXform);
            var distance = Vector2.Distance(npcWorldPos, targetWorldPos);

            if (distance >= closestDistance)
                continue;

            if (!_examine.InRangeUnOccluded(uid, targetUid, sensor.VisionRadius + 0.5f))
                continue;

            if (TryComp<CEMobStateComponent>(targetUid, out var targetMobState)
                && !_mobState.IsAlive(targetUid, targetMobState))
                continue;

            closestDistance = distance;
            closestTarget = targetUid;
        }

        _goap.SetTarget(ent, sensor.OutputTargetKey, closestTarget);
        goap.WorldState[sensor.ConditionKey] = closestTarget != null;
    }
}
