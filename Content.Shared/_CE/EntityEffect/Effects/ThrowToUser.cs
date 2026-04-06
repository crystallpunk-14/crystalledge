using Content.Shared.Projectiles;
using Content.Shared.Throwing;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class ThrowToUser : CEEntityEffectBase<ThrowToUser>
{
    [DataField]
    public float ThrowPower = 10f;
}

public sealed partial class CEThrowToUserEffectSystem : CEEntityEffectSystem<ThrowToUser>
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;

    protected override void Effect(ref CEEntityEffectEvent<ThrowToUser> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } targetEntity)
            return;

        var xform = Transform(args.Args.Source);

        if (TryComp<EmbeddableProjectileComponent>(targetEntity, out var embeddable))
        {
            _projectile.EmbedDetach(targetEntity, embeddable);
        }

        _throwing.TryThrow(targetEntity, xform.Coordinates, args.Effect.ThrowPower);
    }
}
