using Content.Shared._CE.Frost;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class FreezeArea : CEAnimationActionEntry
{
    [DataField]
    public float Radius = 3;

    [DataField]
    public float FallOffFactor = 0.5f;

    [DataField]
    public int MaxStacks = 3;

    public override void Play(EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position)
    {
        EntityCoordinates? targetPoint = null;

        if (target is not null &&
            entManager.TryGetComponent<TransformComponent>(target.Value, out var transformComponent))
            targetPoint = transformComponent.Coordinates;
        else if (position is not null)
            targetPoint = position;

        if (targetPoint is null)
            return;

        var frost = entManager.System<CEFrostSystem>();

        frost.FreezeArea(targetPoint.Value, Radius, FallOffFactor, MaxStacks);
    }
}
