using Content.Shared._CE.Health;
using Robust.Shared.Map;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class Heal: CEEntityEffect
{
    [DataField]
    public int Amount = 1;

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

        var health = entManager.System<CESharedDamageableSystem>();
        health.Heal(target.Value, Amount, user);
    }
}
