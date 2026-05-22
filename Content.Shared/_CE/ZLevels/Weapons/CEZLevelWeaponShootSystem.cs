using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Shared._CE.ZLevels.Weapons;

public sealed class CEZLevelWeaponShootSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = null!;
    [Dependency] private readonly SharedTransformSystem _transform = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelWeaponShootComponent, AmmoShotEvent>(OnZLevelShoot);
    }

    private void OnZLevelShoot(Entity<CEZLevelWeaponShootComponent> entity, ref AmmoShotEvent args)
    {
        if (!TryComp<GunComponent>(entity, out var gun) || gun.ShootCoordinates is not { } target)
            return;

        if (!_zLevels.IsEmptyAtCoordinates(target, out _))
            return;

        ApplyZPhysics(entity, args.FiredProjectiles, target, gun.ProjectileSpeed);
    }

    public void ApplyZPhysics(EntityUid shooter, List<EntityUid> projectiles, EntityCoordinates targetCoords, float speed)
    {
        if (speed <= 0f)
            return;

        if (!_zLevels.IsEmptyAtCoordinates(targetCoords, out _))
            return;

        var shooterPos = _transform.GetMapCoordinates(shooter);
        var targetPos = _transform.ToMapCoordinates(targetCoords);

        var distance = (targetPos.Position - shooterPos.Position).Length();
        if (distance <= 0f)
            return;

        var timeToReach = distance / speed;

        foreach (var projectile in projectiles)
        {
            var zPhys = EnsureComp<CEZPhysicsComponent>(projectile);
            _zLevels.SetZVelocity((projectile, zPhys), -1.25f / timeToReach );
            _zLevels.SetBounciness((projectile, zPhys), 0);
            _zLevels.SetZPosition((projectile, zPhys), 0.25f);
            _zLevels.SetGravityMultiplier((projectile, zPhys), 0);
        }
    }
}
