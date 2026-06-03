using Content.Shared._CE.ZLevels.Damage;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects.FallingImmune;

public sealed class CEFallingImmunityStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEFallingImmunityStatusEffectComponent, StatusEffectRelayedEvent<CEZFallingDamageCalculateEvent>>(OnFall);
    }

    private void OnFall(Entity<CEFallingImmunityStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEZFallingDamageCalculateEvent> args)
    {
        args.Args.DamageMultiplier *= ent.Comp.DamageMultiplier;
        args.Args.StunMultiplier *= ent.Comp.StunMultiplier;
    }
}
