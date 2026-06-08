using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.Health;
using Content.Shared._CE.MeleeWeapon;
using Content.Shared._CE.Soul;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects.Components;
using Content.Shared._CE.TileEffects.Core;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects;

public sealed partial class CEEffectsOnTriggerStatusEffectSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private CEStatusEffectStackSystem _stack = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    [Dependency] private EntityQuery<CEStatusEffectStackComponent> _stackQuery = default!;
    [Dependency] private EntityQuery<StatusEffectComponent> _statusQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEffectOnAttackStatusEffectComponent, StatusEffectRelayedEvent<CEAfterAttackEvent>>(OnAfterAttack);
        SubscribeLocalEvent<CEEffectOnAttackedStatusEffectComponent, StatusEffectRelayedEvent<CEAttackedEvent>>(OnAfterAttacked);
        SubscribeLocalEvent<CEEffectOnHealStatusEffectComponent, StatusEffectRelayedEvent<CEHealEvent>>(OnHeal);
        SubscribeLocalEvent<CEEffectOnDamagedStatusEffectComponent, StatusEffectRelayedEvent<CEDamageChangedEvent>>(OnDamaged);
        SubscribeLocalEvent<CEEffectOnDamageStatusEffectComponent, StatusEffectRelayedEvent<CEAfterDealDamageEvent>>(OnDealDamage);
        SubscribeLocalEvent<CEEffectOnSoulReceivedStatusEffectComponent, StatusEffectRelayedEvent<CESoulReceivedEvent>>(OnSoulReceived);
        SubscribeLocalEvent<CEEffectOnTileApplyStatusEffectComponent, StatusEffectRelayedEvent<CEAttemptApplyTileEffectEvent>>(OnTileApply);
        SubscribeLocalEvent<CEEffectOnStatusEffectApplyStatusEffectComponent, StatusEffectRelayedEvent<CEAfterApplyStatusEffectEvent>>(OnStatusEffectApply);
        SubscribeLocalEvent<CEEffectOnStatusEffectRemoveStatusEffectComponent, StatusEffectRelayedEvent<CEAfterRemoveStatusEffectEvent>>(OnStatusEffectRemove);
        SubscribeLocalEvent<CEEffectOnLandingStatusEffectComponent, StatusEffectRelayedEvent<CEZLevelHitEvent>>(OnLanding);
        SubscribeLocalEvent<CEEffectOnHighSpeedImpactStatusEffectComponent, StatusEffectRelayedEvent<StartCollideEvent>>(OnHighSpeedImpact);
    }

    private void OnAfterAttack(Entity<CEEffectOnAttackStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAfterAttackEvent> args)
    {
        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        if (args.Args.Targets.Count <= 0)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

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
                for (var i = 0; i < stack; i++)
                {
                    effect.Effect(effectArgs);
                }
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnAfterAttacked(Entity<CEEffectOnAttackedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAttackedEvent> args)
    {
        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            status.AppliedTo.Value,
            null,
            Angle.Zero,
            1f,
            args.Args.Attacker,
            Transform(args.Args.Attacker).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnHeal(Entity<CEEffectOnHealStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEHealEvent> args)
    {
        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

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
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnDamaged(Entity<CEEffectOnDamagedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEDamageChangedEvent> args)
    {
        if (args.Args.DamageDelta <= 0)
            return;

        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

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
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnDealDamage(Entity<CEEffectOnDamageStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAfterDealDamageEvent> args)
    {
        if (args.Args.Damage <= 0)
            return;

        if (ent.Comp.AttackTypes.Count > 0 && !ent.Comp.AttackTypes.Contains(args.Args.AttackType))
            return;

        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

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
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnSoulReceived(Entity<CEEffectOnSoulReceivedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CESoulReceivedEvent> args)
    {
        if (args.Args.Amount <= 0)
            return;

        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

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
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnTileApply(Entity<CEEffectOnTileApplyStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAttemptApplyTileEffectEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        if (ent.Comp.SourceTileEffects.Count > 0 && !ent.Comp.SourceTileEffects.Contains(args.Args.TileEffect))
            return;

        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

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
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnStatusEffectApply(Entity<CEEffectOnStatusEffectApplyStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAfterApplyStatusEffectEvent> args)
    {
        if (ent.Comp.SourceStatusEffects.Count > 0 && !ent.Comp.SourceStatusEffects.Contains(args.Args.StatusEffect))
            return;

        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

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
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnLanding(Entity<CEEffectOnLandingStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEZLevelHitEvent> args)
    {
        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            status.AppliedTo.Value,
            null,
            Angle.Zero,
            1f,
            status.AppliedTo.Value,
            Transform(status.AppliedTo.Value).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnStatusEffectRemove(Entity<CEEffectOnStatusEffectRemoveStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAfterRemoveStatusEffectEvent> args)
    {
        if (ent.Comp.SourceStatusEffects.Count > 0 && !ent.Comp.SourceStatusEffects.Contains(args.Args.StatusEffect))
            return;

        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

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
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }

    private void OnHighSpeedImpact(Entity<CEEffectOnHighSpeedImpactStatusEffectComponent> ent, ref StatusEffectRelayedEvent<StartCollideEvent> args)
    {
        if (!args.Args.OurFixture.Hard || !args.Args.OtherFixture.Hard)
            return;

        var speed = args.Args.OurBody.LinearVelocity.Length();
        if (speed < ent.Comp.MinimumSpeed)
            return;

        if (ent.Comp.LastHit != null
            && (_gameTiming.CurTime - ent.Comp.LastHit.Value).TotalSeconds < ent.Comp.DamageCooldown)
            return;

        if (!_statusQuery.TryComp(ent, out var status) || status.AppliedTo is null)
            return;

        ent.Comp.LastHit = _gameTiming.CurTime;

        var stack = 1;
        if (ent.Comp.ScaleWithStacks && _stackQuery.TryComp(ent, out var stackComp))
            stack = stackComp.Stacks;

        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            status.AppliedTo.Value,
            null,
            Angle.Zero,
            1f,
            args.Args.OtherEntity,
            Transform(status.AppliedTo.Value).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            for (var i = 0; i < stack; i++)
            {
                effect.Effect(effectArgs);
            }
        }

        _stack.TryRemoveStack(ent.Owner, ent.Comp.StackCost);
    }
}
