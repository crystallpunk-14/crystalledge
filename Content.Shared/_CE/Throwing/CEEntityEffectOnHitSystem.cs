using Content.Shared._CE.EntityEffect;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;

namespace Content.Shared._CE.Throwable;

public sealed partial class CEEntityEffectOnHitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEntityEffectOnHitComponent, ThrowDoHitEvent>(OnHit);
    }

    private void OnHit(Entity<CEEntityEffectOnHitComponent> ent, ref ThrowDoHitEvent args)
    {
        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            args.Thrown,
            ent,
            Angle.Zero,
            1f,
            args.Target,
            null);

        foreach (var effect in ent.Comp.HitEffects)
        {
            effect.Effect(effectArgs);
        }
    }
}
