using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// GOAP sensor that finds the nearest hostile entity within vision range.
/// Sets the target on the GOAP component and updates the visibility condition.
/// </summary>
public sealed partial class CEGOAPFindEnemySensor : CEGOAPSensorBase<CEGOAPFindEnemySensor>
{
    /// <summary>
    /// Detection range in tiles.
    /// </summary>
    [DataField]
    public float VisionRadius = 10f;

    /// <summary>
    /// Which condition key this sensor updates.
    /// </summary>
    [DataField]
    public ProtoId<CEGOAPConditionPrototype> ConditionKey = "CEEnemyVisible";
}

public sealed partial class CEGOAPFindEnemySensorSystem : CEGOAPSensorSystem<CEGOAPFindEnemySensor>
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPFindEnemySensor> args)
    {
        var conditionKey = (string) args.Sensor.ConditionKey;

        if (!_xformQuery.TryGetComponent(ent, out var xform))
        {
            args.WorldState[conditionKey] = false;
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

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = targetUid;
            }
        }

        if (closestTarget != null)
        {
            ent.Comp.Target = closestTarget;
            args.WorldState[conditionKey] = true;
        }
        else
        {
            ent.Comp.Target = null;
            args.WorldState[conditionKey] = false;
        }
    }
}
