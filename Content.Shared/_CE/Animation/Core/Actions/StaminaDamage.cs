using Content.Shared.Damage.Systems;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class StaminaDamage : CEAnimationActionEntry
{
    [DataField]
    public float Damage = 10f;

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

        var stamina = entManager.System<SharedStaminaSystem>();

        stamina.TakeStaminaDamage(target.Value, Damage, null, user, used);
    }
}
