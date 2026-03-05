using Content.Shared._CE.Health;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class Heal: CEAnimationActionEntry
{
    [DataField]
    public int Amount = 1;

    public override void Play(EntityManager entManager,
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

        var health = entManager.System<CESharedHealthSystem>();
        health.Heal(target.Value, Amount, user);
    }
}
