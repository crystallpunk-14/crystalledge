using Content.Shared.Throwing;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class Dash : CEAnimationActionEntry
{
    [DataField]
    public float Speed = 10f;

    [DataField]
    public float Distance = 1f;

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
        var throwing = entManager.System<ThrowingSystem>();

        throwing.TryThrow(
            user,
            angle.ToWorldVec() * Distance,
            Speed,
            user,
            animated: false,
            doSpin: false);
    }
}
