using System.Linq;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.MeleeWeapon;
using Content.Shared.Interaction;
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
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly CESharedWeaponSystem _melee = default!;

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

        // Find all entities in the arc
        var targets = _lookup.GetEntitiesInArc(
            entityCoords,
            range,
            direction,
            args.Effect.ArcWidth,
            LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries)
            .ToList();

        targets.Remove(args.Args.Source);

        if (args.Args.Used is { } usedEntity)
            targets.Remove(usedEntity);

        // Filter to only damageable entities — skip walls, floor items, etc.
        targets.RemoveAll(t => !HasComp<CEDamageableComponent>(t));

        // Filter out entities behind walls (line-of-sight check).
        // GetEntitiesInArc uses range * 2 for the broadphase lookup, so match that here.
        targets.RemoveAll(t => !_interaction.InRangeUnobstructed(entityCoords, t, range * 2 + 0.1f));

        _melee.HandleArcAttackHit(args.Args.Source, (args.Args.Used.Value, weapon), targets);

        foreach (var target in targets)
        {
            var effectArgs = args.Args with { EntityManager = EntityManager, Target = target, Position = null };

            foreach (var effect in args.Effect.Effects)
            {
                effect.Effect(effectArgs);
            }
        }
    }
}
