using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Damage;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.Skill.Skills.EffectiveHeal;

public sealed partial class CEChangeHealTypeStatusEffectSystem : EntitySystem
{
    [Dependency] private CESharedHealthSystem _health = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEChangeHealTypeStatusEffectComponent, StatusEffectRelayedEvent<CEAttemptHealEvent>>(OnAttemptHeal);
    }

    private void OnAttemptHeal(Entity<CEChangeHealTypeStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAttemptHealEvent> args)
    {
        var targetType = ent.Comp.Target;

        var damage = new CEDamageSpecifier(targetType, args.Args.HealAmount);
        args.Args.Cancel();

        _health.TakeDamage(args.Args.Target, damage, ent);
    }
}
