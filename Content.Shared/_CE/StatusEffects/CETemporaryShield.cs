using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Prototypes;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects;

/// <summary>
/// Status effect component for temporary shields.
/// Each stack absorbs a configurable amount of damage from specific damage types.
/// Stacks passively decay over time via the status effect stack system.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CETempShieldStatusEffectComponent : Component
{
    /// <summary>
    /// How much damage each stack absorbs before being consumed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int AbsorbPerStack = 1;

    /// <summary>
    /// Which damage types this shield absorbs.
    /// Ignored when <see cref="All"/> is true.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<CEDamageTypePrototype>> AbsorbedTypes = new() { "Physical" };

    /// <summary>
    /// If true, absorbs a stack for any damage type, ignoring <see cref="AbsorbedTypes"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool All = false;
}


public sealed partial class CETempShieldSystem : EntitySystem
{
    [Dependency] private CEStatusEffectStackSystem _stacks = default!;

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

            if (!shield.All && shield.AbsorbedTypes.Count > 0 && !shield.AbsorbedTypes.Contains(damageType))
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
