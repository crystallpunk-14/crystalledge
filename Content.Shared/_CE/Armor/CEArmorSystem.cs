using Content.Shared._CE.Health;
using Content.Shared.Inventory;

namespace Content.Shared._CE.Armor;

/// <summary>
///
/// </summary>
public sealed partial class CEArmorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEArmorComponent, CEDamageCalculateEvent>(OnBeforeDamage);
        SubscribeLocalEvent<CEArmorComponent, InventoryRelayedEvent<CEDamageCalculateEvent>>(OnBeforeInventoryDamage);
    }

    private void OnBeforeInventoryDamage(Entity<CEArmorComponent> ent, ref InventoryRelayedEvent<CEDamageCalculateEvent> args)
    {
        OnBeforeDamage(ent, ref args.Args);
    }

    private void OnBeforeDamage(Entity<CEArmorComponent> ent, ref CEDamageCalculateEvent args)
    {
        if (args.Cancelled)
            return;

        var newDamage = new CEDamageSpecifier();

        foreach (var (damageType, damageAmount) in args.Damage.Types)
        {
            var dmg = damageAmount;

            if (ent.Comp.Multiplier.TryGetValue(damageType, out var multiplier))
                dmg = (int)Math.Ceiling(dmg * multiplier);

            if (ent.Comp.Flat.TryGetValue(damageType, out var flat))
                dmg -= flat;

            //Block healing
            dmg = Math.Max(dmg, 0);

            newDamage.Types.Add(damageType, dmg);
        }

        args.Damage = newDamage;
    }
}
