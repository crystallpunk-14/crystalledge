using System.Linq;
using Content.Shared._CE.Weapon;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class WeaponArcAttack : CEAnimationActionEntry
{
    [DataField]
    public float Range = 1.5f;

    [DataField]
    public float ArcWidth = 90f;

    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle, float animationSpeed, TimeSpan frame)
    {
        if (used is null)
            return;

        // Try to use the 'used' weapon if it has a CEMeleeWeaponComponent
        if (!entManager.TryGetComponent<CEMeleeWeaponComponent>(used.Value, out var weapon))
            return;

        var lookup = entManager.System<EntityLookupSystem>();
        var transform = entManager.System<SharedTransformSystem>();
        var melee = entManager.System<CESharedMeleeWeaponSystem>();

        // Get entity coordinates
        var entityCoords = transform.GetMapCoordinates(entity);
        var direction = angle + Angle.FromDegrees(-90);

        var range = Range * weapon.RangeMultiplier;

        // Raise debug event for arc attack visualization
        var debugEvent = new CEItemAttackEvent(entityCoords, direction, range, ArcWidth);
        entManager.EventBus.RaiseEvent(EventSource.Local, debugEvent);

        // Find all entities in the arc
        var targets = lookup.GetEntitiesInArc(
            entityCoords,
            range,
            direction,
            ArcWidth,
            LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries)
            .ToList();

        targets.Remove(entity);
        melee.TryAttack(entity, (used.Value, weapon), targets);
    }
}
