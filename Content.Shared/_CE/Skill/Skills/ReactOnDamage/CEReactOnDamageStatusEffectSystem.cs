using Content.Shared._CE.Health;
using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Skill.Skills.ReactOnDamage;

public sealed partial class CEReactOnDamageStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CESharedHealthSystem _health = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEReactOnDamageStatusEffectComponent, StatusEffectRelayedEvent<CEBeforeDamageEvent>>(OnDamageTaken);
    }

    private void OnDamageTaken(Entity<CEReactOnDamageStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEBeforeDamageEvent> args)
    {
        if (args.Args.Source is null) return;

        EntityUid? target = null;
        var damageType = ent.Comp.DamageType;
        var amount = ent.Comp.Amount;

        switch (ent.Comp.Target)
        {
            case TargetType.Source:
                target = args.Args.Source;
                break;
            case TargetType.Self:
                target = ent;
                break;
            default:
                Log.Warning("No case Defined for this %1", nameof(TargetType));
                return;
        }

        switch (ent.Comp.Reaction)
        {
            case ReactionType.Damage:
                _health.TakeDamage(target.Value, new(damageType, amount));
                break;
            case ReactionType.Heal:
                _health.Heal(target.Value, amount);
                break;
            default:
                Log.Warning("No case Defined for this %1", nameof(ReactionType));
                return;
        }
    }
}
