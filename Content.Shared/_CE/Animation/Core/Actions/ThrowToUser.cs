using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class ThrowToUser : CEAnimationActionEntry
{
    [DataField]
    public float ThrowPower = 10f;

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

        if (!entManager.TryGetComponent<TransformComponent>(user, out var xform))
            return;

        if (entManager.TryGetComponent<EmbeddableProjectileComponent>(targetEntity, out var embeddable))
        {
            var projectile = entManager.System<SharedProjectileSystem>();

            projectile.EmbedDetach(targetEntity, embeddable);
        }

        throwing.TryThrow(targetEntity, xform.Coordinates, ThrowPower);
    }
}
