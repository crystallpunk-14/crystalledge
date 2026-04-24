using Content.Shared._CE.DebuffCleaning;
using Content.Shared._CE.Health;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Skill.Skills.InnocentPurity;

public sealed class CEInnocentPuritySystem : EntitySystem
{
    [Dependency] private readonly CESharedDamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEInnocentPurityComponent, StatusEffectRelayedEvent<CESourceCleanedDebuffsEvent>>(OnSourceCleaned);
    }

    private void OnSourceCleaned(Entity<CEInnocentPurityComponent> ent, ref StatusEffectRelayedEvent<CESourceCleanedDebuffsEvent> args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        if (args.Args.StacksRemoved <= 0)
            return;

        _damageable.Heal(args.Args.Target, args.Args.StacksRemoved * ent.Comp.HealPerStack, status.AppliedTo);
    }
}
