using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared.Examine;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server._CE.GOAP.TargetProviders;

/// <summary>
/// Finds the nearest hostile entity within vision range with line-of-sight check.
/// </summary>
public sealed partial class CEGOAPNearestHostileTargetProvider
    : CEGOAPTargetProviderBase<CEGOAPNearestHostileTargetProvider>
{
    /// <summary>
    /// Detection range in tiles.
    /// </summary>
    [DataField]
    public float VisionRadius = 10f;
}

public sealed partial class CEGOAPNearestHostileTargetProviderSystem
    : CEGOAPTargetProviderSystem<CEGOAPNearestHostileTargetProvider>
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

    protected override void OnResolve(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPResolveTargetEvent<CEGOAPNearestHostileTargetProvider> args)
    {
        if (!_xformQuery.TryGetComponent(ent, out var xform))
            return;

        var npcWorldPos = _transform.GetWorldPosition(xform);
        Entity<NpcFactionMemberComponent?, FactionExceptionComponent?> factionEnt =
            (ent.Owner, null, null);
        var hostiles = _faction.GetNearbyHostiles(factionEnt, args.Provider.VisionRadius);

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
                    args.Provider.VisionRadius + 0.5f))
                continue;

            closestDistance = distance;
            closestTarget = targetUid;
        }

        if (closestTarget != null)
        {
            args.TargetEntity = closestTarget;
            if (_xformQuery.TryGetComponent(closestTarget.Value, out var targetXform))
                args.TargetCoordinates = targetXform.Coordinates;
        }
    }
}
