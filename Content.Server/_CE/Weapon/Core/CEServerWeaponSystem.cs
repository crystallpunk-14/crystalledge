using System.Numerics;
using Content.Server.Movement.Systems;
using Content.Shared._CE.Weapon.Core;
using Content.Shared._CE.Weapon.Core.Components;
using Content.Shared.Effects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._CE.Weapon.Core;

public sealed class CEServerWeaponSystem : CESharedWeaponSystem
{
    [Dependency] private readonly LagCompensationSystem _lag = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;

    protected override bool ArcRaySuccessful(
        EntityUid targetUid,
        Vector2 position,
        Angle angle,
        Angle arcWidth,
        float range,
        MapId mapId,
        EntityUid ignore,
        ICommonSession? session)
    {
        if (!Interaction.InRangeUnobstructed(ignore, targetUid, range + 0.1f, overlapCheck: false))
            return false;

        return true;
    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        EntityCoordinates targetCoordinates;
        Angle targetLocalAngle;

        if (session is { } pSession)
        {
            (targetCoordinates, targetLocalAngle) = _lag.GetCoordinatesAngle(target, pSession);
            return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range,
                overlapCheck: false);
        }

        return Interaction.InRangeUnobstructed(user, target, range);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        var filter = Filter.Pvs(targetXform.Coordinates, entityMan: EntityManager)
            .RemoveWhereAttachedEntity(o => o == user);
        _color.RaiseEffect(Color.Red, targets, filter);
    }

    public override void DoLunge(
        EntityUid user,
        EntityUid weapon,
        Angle angle,
        Vector2 localPos,
        string? animation,
        bool predicted = true)
    {
        Filter filter;

        if (predicted)
            filter = Filter.PvsExcept(user, entityManager: EntityManager);
        else
            filter = Filter.Pvs(user, entityManager: EntityManager);

        RaiseNetworkEvent(
            new CEMeleeLungeEvent(GetNetEntity(user), GetNetEntity(weapon), angle, localPos, animation),
            filter);
    }
}
