using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.MeleeWeapon;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class WeaponArcAttack : CEEntityEffectBase<WeaponArcAttack>
{
    [DataField]
    public float Range = 1.5f;

    [DataField]
    public float ArcWidth = 90f;

    [DataField]
    public Angle Angle = Angle.Zero;

    [DataField]
    public List<CEEntityEffect> Effects = new();
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

public sealed partial class CEWeaponArcAttackEffectSystem : CEEntityEffectSystem<WeaponArcAttack>
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly CESharedWeaponSystem _melee = default!;

    /// <summary>
    /// Broad collision mask to hit mobs, items, machines, etc.
    /// Filtered by CEDamageableComponent afterwards.
    /// </summary>
    private const int ArcAttackMask = (int) (
        CollisionGroup.Opaque |
        CollisionGroup.Impassable |
        CollisionGroup.MidImpassable |
        CollisionGroup.HighImpassable |
        CollisionGroup.LowImpassable |
        CollisionGroup.BulletImpassable);

    /// <summary>
    /// Cast one ray every this many degrees across the arc.
    /// </summary>
    private const float DegreesPerRay = 5f;

    private const int MinRays = 3;

    protected override void Effect(ref CEEntityEffectEvent<WeaponArcAttack> args)
    {
        if (args.Args.Used is null)
            return;

        if (!TryComp<CEWeaponComponent>(args.Args.Used.Value, out var weapon))
            return;

        var entityCoords = _transform.GetMapCoordinates(args.Args.Source);
        var direction = new Angle(args.Args.Angle.ToWorldVec()) + args.Effect.Angle;

        var range = args.Effect.Range;

        // Raise debug event for arc attack visualization
        var debugEvent = new CEDebugArcAttackEvent(entityCoords, direction, range, args.Effect.ArcWidth);
        EntityManager.EventBus.RaiseEvent(EventSource.Local, debugEvent);

        // Fan rays across the arc — same approach as vanilla MeleeWeaponSystem.ArcRayCast,
        // but with adaptive ray count: 1 ray per DegreesPerRay, minimum MinRays.
        // Note: range * 2 matches the original GetEntitiesInArc broadphase radius.
        var effectiveRange = range * 2;
        var arcWidthDeg = args.Effect.ArcWidth;
        var arcWidthRad = arcWidthDeg * Math.PI / 180.0;
        var rayCount = Math.Max(MinRays, (int) Math.Ceiling(arcWidthDeg / DegreesPerRay) + 1);
        var baseAngle = direction.Theta - arcWidthRad / 2;
        var increment = arcWidthRad / (rayCount - 1);

        var hitEntities = new HashSet<EntityUid>();

        for (var i = 0; i < rayCount; i++)
        {
            var castAngle = new Angle(baseAngle + increment * i);
            var ray = new CollisionRay(entityCoords.Position, castAngle.ToVec(), ArcAttackMask);

            foreach (var result in _physics.IntersectRay(
                         entityCoords.MapId, ray, effectiveRange, args.Args.Source, false))
            {
                hitEntities.Add(result.HitEntity);
            }
        }

        hitEntities.Remove(args.Args.Source);
        if (args.Args.Used is { } usedEntity)
            hitEntities.Remove(usedEntity);

        // Filter to only damageable entities.
        hitEntities.RemoveWhere(t => !HasComp<CEDamageableComponent>(t));

        // Filter out entities behind walls (line-of-sight check).
        hitEntities.RemoveWhere(t =>
            !_interaction.InRangeUnobstructed(entityCoords, t, effectiveRange + 0.1f));

        var targets = new List<EntityUid>(hitEntities);

        // Find which EffectSlot on the weapon contains this arc attack.
        // The server uses this to replay nested effects on validated targets.
        var effectSlot = FindEffectSlot(weapon, args.Effect);

        // Server clears targets for player attacks (damage goes through CEWeaponArcHitEvent).
        _melee.HandleArcAttackHit(args.Args.Source, (args.Args.Used.Value, weapon), targets, effectSlot);
    }

    /// <summary>
    /// Finds the EffectSlot key that contains the given WeaponArcAttack instance.
    /// </summary>
    private static string? FindEffectSlot(CEWeaponComponent weapon, WeaponArcAttack effect)
    {
        foreach (var (key, effects) in weapon.EffectSlots)
        {
            foreach (var e in effects)
            {
                if (ReferenceEquals(e, effect))
                    return key;
            }
        }

        return null;
    }
}
