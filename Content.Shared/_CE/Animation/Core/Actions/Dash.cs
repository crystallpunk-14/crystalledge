using Content.Shared.Throwing;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class Dash : CEAnimationActionEntry
{
    [DataField]
    public float Speed = 10f;

    [DataField]
    public float Distance = 1f;

    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle, float animationSpeed, TimeSpan frame)
    {
        var throwing = entManager.System<ThrowingSystem>();

        throwing.TryThrow(
            entity,
            angle.ToWorldVec() * Distance,
            Speed,
            entity,
            animated: false,
            doSpin: false);
    }
}
