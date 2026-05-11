using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.Health;
using Content.Shared._CE.MeleeWeapon;
using Content.Shared._CE.Skill.Skills.EffectsOnTriggerStatusEffects.Components;
using Content.Shared._CE.Soul;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.TileEffects.Core;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Whitelist;

namespace Content.Shared._CE.Skill.Skills.EffectsOnTriggerStatusEffects;

public sealed class CEEffectsOnTriggerStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEffectOnAttackStatusEffectComponent, StatusEffectRelayedEvent<CEAfterAttackEvent>>(OnAfterAttack);
        SubscribeLocalEvent<CEEffectOnHealStatusEffectComponent, StatusEffectRelayedEvent<CEHealEvent>>(OnHeal);
        SubscribeLocalEvent<CEEffectOnDamagedStatusEffectComponent, StatusEffectRelayedEvent<CEDamageChangedEvent>>(OnDamaged);
        SubscribeLocalEvent<CEEffectOnDamageStatusEffectComponent, StatusEffectRelayedEvent<CEAfterDealDamageEvent>>(OnDealDamage);
        SubscribeLocalEvent<CEEffectOnSoulReceivedStatusEffectComponent, StatusEffectRelayedEvent<CESoulReceivedEvent>>(OnSoulReceived);
        SubscribeLocalEvent<CEEffectOnTileApplyStatusEffectComponent, StatusEffectRelayedEvent<CEAttemptApplyTileEffectEvent>>(OnTileApply);
        SubscribeLocalEvent<CEEffectOnStatusEffectApplyStatusEffectComponent, StatusEffectRelayedEvent<CEAfterApplyStatusEffectEvent>>(OnStatusEffectApply);
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

    private void OnHeal(Entity<CEEffectOnHealStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEHealEvent> args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            status.AppliedTo.Value,
            null,
            Angle.Zero,
            1f,
            args.Args.Target,
            Transform(args.Args.Target).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnDamaged(Entity<CEEffectOnDamagedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEDamageChangedEvent> args)
    {
        if (args.Args.DamageDelta <= 0)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            status.AppliedTo.Value,
            null,
            Angle.Zero,
            1f,
            args.Args.Source,
            Transform(status.AppliedTo.Value).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnDealDamage(Entity<CEEffectOnDamageStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAfterDealDamageEvent> args)
    {
        if (args.Args.Damage <= 0)
            return;

        if (ent.Comp.AttackTypes.Count > 0 && !ent.Comp.AttackTypes.Contains(args.Args.AttackType))
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            status.AppliedTo.Value,
            args.Args.Weapon,
            Angle.Zero,
            1f,
            args.Args.Target,
            Transform(args.Args.Target).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnSoulReceived(Entity<CEEffectOnSoulReceivedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CESoulReceivedEvent> args)
    {
        if (args.Args.Amount <= 0)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            status.AppliedTo.Value,
            null,
            Angle.Zero,
            1f,
            null,
            Transform(status.AppliedTo.Value).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnTileApply(Entity<CEEffectOnTileApplyStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAttemptApplyTileEffectEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        if (ent.Comp.SourceTileEffects.Count > 0 && !ent.Comp.SourceTileEffects.Contains(args.Args.TileEffect))
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            status.AppliedTo.Value,
            null,
            Angle.Zero,
            1f,
            null,
            args.Args.Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnStatusEffectApply(Entity<CEEffectOnStatusEffectApplyStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAfterApplyStatusEffectEvent> args)
    {
        if (ent.Comp.SourceStatusEffects.Count > 0 && !ent.Comp.SourceStatusEffects.Contains(args.Args.StatusEffect))
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var status) || status.AppliedTo is null)
            return;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            status.AppliedTo.Value,
            null,
            Angle.Zero,
            1f,
            args.Args.Target,
            Transform(args.Args.Target).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }
}
