using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.TempShield;

public sealed class CETempShieldSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stacks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CETempShieldStatusEffectComponent, StatusEffectRelayedEvent<CEDamageCalculateEvent>>(OnBeforeDamage);
    }

    private void OnBeforeDamage(Entity<CETempShieldStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEDamageCalculateEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        if (!TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect) || statusEffect.AppliedTo is null)
            return;

        var shield = ent.Comp;
        var currentStacks = stackComp.Stacks;
        var absorbBudget = currentStacks * shield.AbsorbPerStack;
        var totalAbsorbed = 0;

        var newDamage = new CEDamageSpecifier();
        foreach (var (damageType, damageAmount) in args.Args.Damage.Types)
        {
            if (damageAmount <= 0)
            {
                newDamage.Types[damageType] = damageAmount;
                continue;
            }

            if (shield.AbsorbedTypes.Count > 0 && !shield.AbsorbedTypes.Contains(damageType))
            {
                newDamage.Types[damageType] = damageAmount;
                continue;
            }

            var absorbed = Math.Min(damageAmount, absorbBudget);
            absorbBudget -= absorbed;
            totalAbsorbed += absorbed;

            var remaining = damageAmount - absorbed;
            if (remaining > 0)
                newDamage.Types[damageType] = remaining;
        }

        if (totalAbsorbed <= 0)
            return;

        var stacksConsumed = (int) Math.Ceiling((double) totalAbsorbed / shield.AbsorbPerStack);
        stacksConsumed = Math.Min(stacksConsumed, currentStacks);

        _stacks.TryRemoveStack(ent.Owner, stacksConsumed);

        if (newDamage.Total <= 0)
            args.Args.Cancelled = true;
        else
            args.Args.Damage = newDamage;
    }
}
