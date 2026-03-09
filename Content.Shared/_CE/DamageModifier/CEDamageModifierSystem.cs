using Content.Shared._CE.Health;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.DamageModifier;

/// <summary>
/// Processes <see cref="CEDamageModifierStatusEffectComponent"/> on status effect entities.
/// Intercepts incoming damage via the status effect relay and applies flat then multiplier modifiers.
/// </summary>
public sealed class CEDamageModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageModifierStatusEffectComponent, StatusEffectRelayedEvent<CEBeforeDamageEvent>>(OnBeforeDamage);
    }

    private void OnBeforeDamage(
        Entity<CEDamageModifierStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CEBeforeDamageEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        var damage = args.Args.Damage;

        // Apply flat modifiers first, then multipliers.
        foreach (var (type, flat) in ent.Comp.FlatModifiers)
        {
            if (!damage.Types.TryGetValue(type, out var current))
                continue;

            damage.Types[type] = Math.Max(0, current + flat);
        }

        foreach (var (type, mult) in ent.Comp.Multipliers)
        {
            if (!damage.Types.TryGetValue(type, out var current))
                continue;

            damage.Types[type] = Math.Max(0, (int)(current * mult));
        }

        // If total damage is now 0 or less, cancel the event entirely.
        if (damage.Total <= 0)
            args.Args.Cancelled = true;
    }
}
