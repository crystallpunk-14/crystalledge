using Content.Shared._CE.DivineShield;
using Content.Shared._CE.EntityEffect;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.Skill.Skills.DivineShieldBreakEffect;

public sealed class CEDivineShieldBreakEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDivineShieldBreakEffectComponent, StatusEffectRelayedEvent<CEDivineShieldBrokenEvent>>(OnDivineShieldBroken);
    }

    private void OnDivineShieldBroken(Entity<CEDivineShieldBreakEffectComponent> ent, ref StatusEffectRelayedEvent<CEDivineShieldBrokenEvent> args)
    {
        if (!args.Args.RaisedOnApplier)
            return;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            args.Args.Applier ?? args.Args.ShieldHolder,
            null,
            Angle.Zero,
            0f,
            args.Args.ShieldHolder,
            Transform(args.Args.ShieldHolder).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }
    }
}
