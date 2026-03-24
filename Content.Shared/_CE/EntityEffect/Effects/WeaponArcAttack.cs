using System.Linq;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.MeleeWeapon;
using Robust.Shared.Map;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class WeaponArcAttack : CEEntityEffectBase<WeaponArcAttack>
{
    [DataField]
    public float Range = 1.5f;

    [DataField]
    public float ArcWidth = 90f;

    [DataField]
    public Angle Angle = Angle.Zero;

    /// <summary>
    /// The overall damage modifier for this attack.
    /// </summary>
    [DataField]
    public float Power = 1f;
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
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CESharedWeaponSystem _melee = default!;

    protected override void Effect(ref CEEntityEffectEvent<WeaponArcAttack> args)
    {
        if (args.Args.Used is null)
            return;

        if (!TryComp<CEWeaponComponent>(args.Args.Used.Value, out var weapon))
            return;

        var entityCoords = _transform.GetMapCoordinates(args.Args.User);
        var direction = new Angle(args.Args.Angle.ToWorldVec()) + args.Effect.Angle;

        var range = args.Effect.Range * weapon.RangeMultiplier;

        // Raise debug event for arc attack visualization
        var debugEvent = new CEDebugArcAttackEvent(entityCoords, direction, range, args.Effect.ArcWidth);
        EntityManager.EventBus.RaiseEvent(EventSource.Local, debugEvent);

        // Find all entities in the arc
        var targets = _lookup.GetEntitiesInArc(
            entityCoords,
            range,
            direction,
            args.Effect.ArcWidth,
            LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries)
            .ToList();

        targets.Remove(args.Args.User);
        _melee.HandleArcAttackHit(args.Args.User, (args.Args.Used.Value, weapon), targets, args.Effect.Power);
    }
}
