using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared.Examine;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Finds the nearest hostile entity within vision range.
/// Sets the target on the GOAP component and updates the visibility condition.
/// </summary>
public sealed partial class CEGOAPFindEnemySensor : CEGOAPSensorBase<CEGOAPFindEnemySensor>
{
    /// <summary>
    /// Detection range in tiles.
    /// </summary>
    [DataField]
    public float VisionRadius = 10f;
}

public sealed partial class CEGOAPFindEnemySensorSystem : CEGOAPSensorSystem<CEGOAPFindEnemySensor>
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPFindEnemySensor> args)
    {
        if (!_xformQuery.TryGetComponent(ent, out var xform))
        {
            SetState(ref args, false);
            ent.Comp.Target = null;
            return;
        }

        var npcWorldPos = _transform.GetWorldPosition(xform);
        Entity<NpcFactionMemberComponent?, FactionExceptionComponent?> factionEnt = (ent.Owner, null, null);
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

            // Line-of-sight check: can we see this target through walls?
            if (!_examine.InRangeUnOccluded(ent.Owner, targetUid, args.Sensor.VisionRadius + 0.5f))
                continue;

            closestDistance = distance;
            closestTarget = targetUid;
        }

        if (closestTarget != null)
        {
            ent.Comp.Target = closestTarget;
            SetState(ref args, true);
        }
        else
        {
            ent.Comp.Target = null;
            SetState(ref args, false);
        }
    }
}
