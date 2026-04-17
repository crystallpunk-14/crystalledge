using Content.Shared._CE.EntityEffect;
using Robust.Shared.Physics.Events;

namespace Content.Server._CE.EntityEffect;

public sealed class CEEntityEffectOnCollideSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEEntityEffectOnCollideComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<CEEntityEffectOnCollideComponent> ent, ref StartCollideEvent args)
    {
        foreach (var effect in ent.Comp.Effects)
        {
            var effectArgs = new CEEntityEffectArgs(
                EntityManager,
                Source: ent,
                Used: null,
                Angle: Angle.Zero,
                Speed: 0f,
                Target: args.OtherEntity,
                Position: null);

            effect.Effect(effectArgs);
        }
    }
}
