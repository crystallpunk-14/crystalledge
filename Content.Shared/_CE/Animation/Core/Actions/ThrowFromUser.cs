using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class ThrowFromUser : CEAnimationActionEntry
{
    [DataField]
    public float ThrowPower = 10f;

    [DataField]
    public float Distance = 2.5f;

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
        if (target is null)
            return;

        var targetEntity = target.Value;

        var throwing = entManager.System<ThrowingSystem>();
        var xform = entManager.System<SharedTransformSystem>();

        var worldPos = xform.GetWorldPosition(user);
        var dir = xform.GetWorldPosition(target.Value) - worldPos;
        if (dir == Vector2.Zero)
            return;

        var foo = Vector2.Normalize(dir);

        if (entManager.TryGetComponent<EmbeddableProjectileComponent>(targetEntity, out var embeddable))
        {
            var projectile = entManager.System<SharedProjectileSystem>();

            projectile.EmbedDetach(targetEntity, embeddable);
        }

        throwing.TryThrow(targetEntity, foo * Distance, ThrowPower, user, doSpin: true);
    }
}
