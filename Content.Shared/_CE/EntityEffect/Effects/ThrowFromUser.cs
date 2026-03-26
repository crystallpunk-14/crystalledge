using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class ThrowFromUser : CEEntityEffectBase<ThrowFromUser>
{
    [DataField]
    public float ThrowPower = 10f;

    [DataField]
    public float Distance = 2.5f;
}

public sealed partial class CEThrowFromUserEffectSystem : CEEntityEffectSystem<ThrowFromUser>
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;

    protected override void Effect(ref CEEntityEffectEvent<ThrowFromUser> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } targetEntity)
            return;

        var worldPos = _transform.GetWorldPosition(args.Args.User);
        var dir = _transform.GetWorldPosition(targetEntity) - worldPos;
        if (dir == Vector2.Zero)
            return;

        var normalized = Vector2.Normalize(dir);

        if (TryComp<EmbeddableProjectileComponent>(targetEntity, out var embeddable))
        {
            _projectile.EmbedDetach(targetEntity, embeddable);
        }

        _throwing.TryThrow(targetEntity, normalized * args.Effect.Distance, args.Effect.ThrowPower, args.Args.User, doSpin: true);
    }
}
