using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Throws the target entity outward from the explosion/area center stored in <see cref="CEEntityEffectArgs.Position"/>.
/// Falls back to the caster's position when <c>Position</c> is not set.
/// Intended to be used as a nested effect inside <see cref="AreaEffect"/> for radial knockback.
/// </summary>
public sealed partial class ThrowFromPosition : CEEntityEffectBase<ThrowFromPosition>
{
    [DataField]
    public float ThrowPower = 10f;

    [DataField]
    public float Distance = 2.5f;
}

public sealed partial class CEThrowFromPositionEffectSystem : CEEntityEffectSystem<ThrowFromPosition>
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;

    protected override void Effect(ref CEEntityEffectEvent<ThrowFromPosition> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } targetEntity)
            return;

        Vector2 fromWorldPos;
        if (args.Args.Position is not null)
        {
            fromWorldPos = _transform.ToMapCoordinates(args.Args.Position.Value).Position;
        }
        else
        {
            fromWorldPos = _transform.GetWorldPosition(args.Args.Source);
        }

        var targetWorldPos = _transform.GetWorldPosition(targetEntity);
        var dir = targetWorldPos - fromWorldPos;
        if (dir == Vector2.Zero)
            return;

        var normalized = Vector2.Normalize(dir);

        if (TryComp<EmbeddableProjectileComponent>(targetEntity, out var embeddable))
        {
            _projectile.EmbedDetach(targetEntity, embeddable);
        }

        _throwing.TryThrow(targetEntity, normalized * args.Effect.Distance, args.Effect.ThrowPower, args.Args.Source, doSpin: true);
    }
}
