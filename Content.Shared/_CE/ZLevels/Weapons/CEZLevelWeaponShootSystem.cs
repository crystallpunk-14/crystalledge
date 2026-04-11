using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.ZLevels.Utilities;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._CE.ZLevels.Weapons;

public sealed class CEZLevelWeaponShootSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = null!;
    [Dependency] private readonly CEZLevelShootHelperSystem _zHelper = null!;

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

        _zHelper.ApplyZPhysics(entity, args.FiredProjectiles, target, gun.ProjectileSpeed);
    }
}
