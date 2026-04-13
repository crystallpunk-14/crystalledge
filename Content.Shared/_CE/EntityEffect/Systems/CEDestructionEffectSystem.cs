using Content.Shared._CE.Health;

namespace Content.Shared._CE.EntityEffect.Systems;

public sealed class CEDestructionEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEDestructionEffectComponent, CEDestructedEvent>(OnDestructed);
    }

    private void OnDestructed(Entity<CEDestructionEffectComponent> ent, ref CEDestructedEvent args)
    {
        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            ent.Owner,
            null,
            Angle.Zero,
            0f,
            null,
            args.Position);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }
    }
}
