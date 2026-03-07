using System.Numerics;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class ShootProjectile : CEAnimationActionEntry
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public float ProjectileSpeed = 20f;

    [DataField]
    public float Spread;

    [DataField]
    public int ProjectileCount = 1;

    [DataField]
    public bool SaveVelocity;

    public override void Play(
        EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position)
    {
        EntityCoordinates? targetPoint = null;

        if (target is not null &&
            entManager.TryGetComponent<TransformComponent>(target.Value, out var transformComponent))
            targetPoint = transformComponent.Coordinates;
        else if (position is not null)
            targetPoint = position;

        if (targetPoint is null)
            return;

        var transform = entManager.System<SharedTransformSystem>();
        var physics = entManager.System<SharedPhysicsSystem>();
        var gunSystem = entManager.System<SharedGunSystem>();
        var mapManager = IoCManager.Resolve<IMapManager>();
        var netManager = IoCManager.Resolve<INetManager>();
        var random = IoCManager.Resolve<IRobustRandom>();

        if (!netManager.IsServer)
            return;

        if (!entManager.TryGetComponent<TransformComponent>(user, out var xform))
            return;

        var fromCoords = xform.Coordinates;
        var userVelocity = physics.GetMapLinearVelocity(user);

        // If applicable, this ensures the projectile is parented to grid on spawn, instead of the map.
        var fromMap = transform.ToMapCoordinates(fromCoords);

        var spawnCoords = mapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? transform.WithEntityId(fromCoords, gridUid)
            : new(mapManager.GetMapEntityId(fromMap.MapId), fromMap.Position);

        for (var i = 0; i < ProjectileCount; i++)
        {
            //Apply spread to target point
            var offsetedTargetPoint = targetPoint.Value.Offset(new Vector2(
                (float)(random.NextDouble() * 2 - 1) * Spread,
                (float)(random.NextDouble() * 2 - 1) * Spread));

            if (fromCoords == offsetedTargetPoint)
                continue;

            var ent = entManager.SpawnAtPosition(Prototype, spawnCoords);

            var direction = offsetedTargetPoint.ToMapPos(entManager, transform) -
                            spawnCoords.ToMapPos(entManager, transform);

            gunSystem.ShootProjectile(ent,
                direction,
                SaveVelocity ? userVelocity : new Vector2(),
                user,
                user,
                ProjectileSpeed);
        }
    }
}
