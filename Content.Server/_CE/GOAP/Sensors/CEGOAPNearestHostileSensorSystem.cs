using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Examine;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Finds the nearest hostile entity within vision range with line-of-sight check.
/// Sets the condition to true if a hostile is found and writes it to Targets[OutputTargetKey].
/// </summary>
public sealed partial class CEGOAPNearestHostileSensor : CEGOAPSensorBase<CEGOAPNearestHostileSensor>
{
    public override TimeSpan? UpdateInterval => TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Detection range in tiles.
    /// </summary>
    [DataField]
    public float VisionRadius = 10f;

    /// <summary>
    /// Key in CEGOAPComponent.Targets to write the resolved target entity into.
    /// </summary>
    [DataField(required: true)]
    public string OutputTargetKey = string.Empty;
}

public sealed partial class CEGOAPNearestHostileSensorSystem
    : CEGOAPSensorSystem<CEGOAPNearestHostileSensor>
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;

    protected override bool? OnSensorUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPSensorUpdateEvent<CEGOAPNearestHostileSensor> args)
    {
        if (!_xformQuery.TryGetComponent(ent, out var xform))
        {
            Goap.SetTarget(ent, args.Sensor.OutputTargetKey, null);
            return false;
        }

        var npcWorldPos = _transform.GetWorldPosition(xform);
        Entity<NpcFactionMemberComponent?, FactionExceptionComponent?> factionEnt =
            (ent.Owner, null, null);
        var hostiles = _faction.GetNearbyHostiles(factionEnt, args.Sensor.VisionRadius);

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

            // Line-of-sight check
            if (!_examine.InRangeUnOccluded(
                    ent.Owner,
                    targetUid,
                    args.Sensor.VisionRadius + 0.5f))
                continue;

            // Only skip entities that explicitly have CEMobStateComponent and are NOT alive.
            // Vanilla mobs (MobState only, no CEMobStateComponent) are treated as alive.
            if (TryComp<CEMobStateComponent>(targetUid, out var targetMobState)
                && !_mobState.IsAlive(targetUid, targetMobState))
                continue;

            closestDistance = distance;
            closestTarget = targetUid;
        }

        Goap.SetTarget(ent, args.Sensor.OutputTargetKey, closestTarget);
        return closestTarget != null;
    }
}
