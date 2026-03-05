using System.Linq;
using Content.Shared._CE.Animation.Item;
using Content.Shared._CE.Animation.Item.Components;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class WeaponArcAttack : CEAnimationActionEntry
{
    [DataField]
    public float Range = 1.5f;

    [DataField]
    public float ArcWidth = 90f;

    /// <summary>
    /// The overall damage modifier for this attack.
    /// </summary>
    [DataField]
    public float Power = 1f;

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
        if (used is null)
            return;

        // Try to use the 'used' weapon if it has a CEMeleeWeaponComponent
        if (!entManager.TryGetComponent<CEWeaponComponent>(used.Value, out var weapon))
            return;

        var lookup = entManager.System<EntityLookupSystem>();
        var transform = entManager.System<SharedTransformSystem>();
        var melee = entManager.System<CESharedWeaponSystem>();

        // Get entity coordinates
        var entityCoords = transform.GetMapCoordinates(user);
        var direction = new Angle(angle.ToWorldVec());

        var range = Range * weapon.RangeMultiplier;

        // Raise debug event for arc attack visualization
        var debugEvent = new CEDebugArcAttackEvent(entityCoords, direction, range, ArcWidth);
        entManager.EventBus.RaiseEvent(EventSource.Local, debugEvent);

        // Find all entities in the arc
        var targets = lookup.GetEntitiesInArc(
            entityCoords,
            range,
            direction,
            ArcWidth,
            LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries)
            .ToList();

        targets.Remove(user);
        melee.TryAttack(user, (used.Value, weapon), targets, Power);
    }
}

/// <summary>
/// Local event raised when an ArcAttack fires, used for debug visualization.
/// </summary>
public sealed class CEDebugArcAttackEvent(MapCoordinates position, Angle direction, float range, float arcWidth)
    : EntityEventArgs
{
    public MapCoordinates Position = position;
    public Angle Direction = direction;
    public float Range = range;
    public float ArcWidth = arcWidth;
}
