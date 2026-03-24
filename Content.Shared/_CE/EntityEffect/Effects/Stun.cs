using Content.Shared.Stunnable;
using Robust.Shared.Map;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class Stun: CEEntityEffect
{
    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.FromSeconds(1f);

    [DataField]
    public bool DropItems = false;

    public override void Effect(EntityManager entManager,
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

        var stun = entManager.System<SharedStunSystem>();

        stun.TryKnockdown(target.Value, Duration, drop: DropItems);
        stun.TryAddStunDuration(target.Value, Duration);
    }
}
