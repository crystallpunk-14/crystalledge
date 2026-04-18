using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.Skills.DanceOfFireAndIce;

public sealed class CEDanceOfFireAndIceSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDanceOfFireAndIceComponent, StatusEffectRelayedEvent<CEOutgoingDamageCalculateEvent>>(OnOutgoingDamage);
    }

    private void OnOutgoingDamage(
        Entity<CEDanceOfFireAndIceComponent> ent,
        ref StatusEffectRelayedEvent<CEOutgoingDamageCalculateEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        var target = args.Args.Target;

        // Bonus fire damage vs frozen targets
        if (_statusEffect.HasStatusEffect(target, ent.Comp.FrozenEffect)
            && args.Args.Damage.Types.TryGetValue("Fire", out var fireDmg)
            && fireDmg > 0)
        {
            args.Args.Damage.Types["Fire"] = fireDmg + ent.Comp.FireBonusVsFrozen;
        }

        // Bonus cold damage vs burning targets
        if (_statusEffect.HasStatusEffect(target, ent.Comp.BurningEffect)
            && args.Args.Damage.Types.TryGetValue("Cold", out var coldDmg)
            && coldDmg > 0)
        {
            args.Args.Damage.Types["Cold"] = coldDmg + ent.Comp.ColdBonusVsBurning;
        }
    }
}
