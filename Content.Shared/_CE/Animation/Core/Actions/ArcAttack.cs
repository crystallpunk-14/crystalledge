using System.Linq;
using Content.Shared._CE.Weapon;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class ArcAttack : CEAnimationActionEntry
{
    [DataField]
    public float Range = 1.5f;

    [DataField]
    public float ArcWidth = 90f;

    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle, TimeSpan frame)
    {
        if (used is null)
            return;

        // Try to use the 'used' weapon if it has a CEMeleeWeaponComponent
        if (!entManager.TryGetComponent<CEMeleeWeaponComponent>(used.Value, out var weapon))
            return;

        var lookup = entManager.System<EntityLookupSystem>();
        var transform = entManager.System<SharedTransformSystem>();
        var ceMelee = entManager.System<CEMeleeWeaponSystem>();

        // Get entity coordinates
        var entityCoords = transform.GetMapCoordinates(entity);

        // Find all entities in the arc
        var targets = lookup.GetEntitiesInArc(
            entityCoords,
            Range,
            angle + Angle.FromDegrees(-90),
            ArcWidth,
            LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries).ToList();

        targets.Remove(entity);
        ceMelee.TryAttack(entity, (used.Value, weapon), targets);
    }
}
