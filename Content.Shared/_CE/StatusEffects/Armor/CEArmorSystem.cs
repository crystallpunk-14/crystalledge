using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects.Armor;

public sealed partial class CEArmorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEArmorComponent, CEDamageCalculateEvent>(OnBeforeDamage);
        SubscribeLocalEvent<CEArmorComponent, StatusEffectRelayedEvent<CEDamageCalculateEvent>>(OnBeforeStatusDamage);
    }

    private void OnBeforeStatusDamage(Entity<CEArmorComponent> ent, ref StatusEffectRelayedEvent<CEDamageCalculateEvent> args)
    {
        var stack = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stacks))
            stack = stacks.Stacks;

        args.Args.Damage = GetNewDamage(args.Args.Damage, ent, stack);
    }

    private void OnBeforeDamage(Entity<CEArmorComponent> ent, ref CEDamageCalculateEvent args)
    {
        if (args.Cancelled)
            return;

        args.Damage = GetNewDamage(args.Damage, ent);
    }

    private CEDamageSpecifier GetNewDamage(CEDamageSpecifier originalDamage, CEArmorComponent armor, int armorStack = 1)
    {
        var newDamage = new CEDamageSpecifier();

        foreach (var (damageType, damageAmount) in originalDamage.Types)
        {
            if (damageAmount <= 0)
                continue;

            var dmg = damageAmount;

            for (var i = 0; i < armorStack; i++)
            {
                if (armor.Multiplier.TryGetValue(damageType, out var multiplier))
                    dmg = (int)Math.Ceiling(dmg * multiplier);

                if (armor.Flat.TryGetValue(damageType, out var flat))
                    dmg -= flat;
            }

            dmg = Math.Max(dmg, 0);

            newDamage.Types.Add(damageType, dmg);
        }

        return newDamage;
    }
}
