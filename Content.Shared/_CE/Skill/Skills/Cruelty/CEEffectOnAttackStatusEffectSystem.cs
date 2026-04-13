using Content.Shared._CE.DivineShield;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.MeleeWeapon;
using Content.Shared._CE.Skill.Skills.DivineShieldBreakEffect;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Whitelist;

namespace Content.Shared._CE.Skill.Skills.Cruelty;

public sealed class CEEffectOnAttackStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEffectOnAttackStatusEffectComponent, StatusEffectRelayedEvent<CEAfterAttackEvent>>(OnAfterAttack);
    }

    private void OnAfterAttack(Entity<CEEffectOnAttackStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAfterAttackEvent> args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        if (args.Args.Targets.Count <= 0)
            return;

        foreach (var target in args.Args.Targets)
        {
            if (!_whitelist.CheckBoth(target, ent.Comp.Blacklist, ent.Comp.Whitelist))
                continue;

            var effectArgs = new CEEntityEffectArgs(
                EntityManager,
                status.AppliedTo.Value,
                args.Args.Weapon,
                Angle.Zero,
                1f,
                target,
                Transform(target).Coordinates);

            foreach (var effect in ent.Comp.Effects)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }
}
