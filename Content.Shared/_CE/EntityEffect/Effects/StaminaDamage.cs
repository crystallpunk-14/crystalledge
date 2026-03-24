using Content.Shared.Damage.Systems;
using Robust.Shared.Map;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class StaminaDamage : CEEntityEffect
{
    [DataField]
    public float Damage = 10f;

    public override void Effect(
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
