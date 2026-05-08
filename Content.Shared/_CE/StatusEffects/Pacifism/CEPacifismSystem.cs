using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.Health;
using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Procedural.Components;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared.Mind.Components;
using Content.Shared.Prototypes;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.Pacifism;

/// <summary>
/// While the pacifism status effect is active, three protections fire:
///   * outgoing damage from the pacified entity to player targets is reduced to zero;
///   * outgoing application of negative status effects (those marked with
///     <see cref="CENegativeStatusEffectComponent"/>) to player targets is cancelled;
///   * outgoing mana theft (any <see cref="CESharedMagicEnergySystem.TransferEnergy"/> where
///     the sender is a player) is cancelled.
/// Item use, attacks, throws and ability casts are NOT blocked any more — only the harmful
/// PvP outcomes are. PvE damage, PvE debuffs and PvE mana drains are unaffected.
/// </summary>
public sealed class CEPacifismSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    [Dependency] private readonly EntityQuery<CEDungeonPlayerComponent> _playerQuery = default!;
    [Dependency] private readonly EntityQuery<StatusEffectComponent> _statusEffectQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEPacifismStatusEffectComponent, StatusEffectRelayedEvent<CEOutgoingDamageCalculateEvent>>(OnOutgoingDamage);
        SubscribeLocalEvent<CEPacifismStatusEffectComponent, StatusEffectRelayedEvent<CEAttemptApplyStatusEffectEvent>>(OnAttemptApplyStatusEffect);
        SubscribeLocalEvent<CEPacifismStatusEffectComponent, StatusEffectRelayedEvent<CEAttemptApplyStatusEffectStackEvent>>(OnAttemptApplyStatusEffectStack);
        SubscribeLocalEvent<CEPacifismStatusEffectComponent, StatusEffectRelayedEvent<CEAttemptStealManaEvent>>(OnAttemptStealMana);
    }

    private void OnOutgoingDamage(
        Entity<CEPacifismStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CEOutgoingDamageCalculateEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        if (!_statusEffectQuery.TryComp(ent, out var statusEffect))
            return;

        if (statusEffect.AppliedTo is null)
            return;

        if (args.Args.Target == statusEffect.AppliedTo) //We are not block self-harm
            return;

        if (!_playerQuery.HasComp(args.Args.Target))
            return;

        args.Args.Cancelled = true;
    }

    private void OnAttemptApplyStatusEffect(
        Entity<CEPacifismStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CEAttemptApplyStatusEffectEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        if (ShouldBlock(args.Args.Target, args.Args.StatusEffect))
            args.Args.Cancelled = true;
    }

    private void OnAttemptApplyStatusEffectStack(
        Entity<CEPacifismStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CEAttemptApplyStatusEffectStackEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        if (ShouldBlock(args.Args.Target, args.Args.StatusEffect))
            args.Args.Cancelled = true;
    }

    private bool ShouldBlock(EntityUid target, EntProtoId statusEffect)
    {
        if (!HasComp<CEDungeonPlayerComponent>(target))
            return false;

        if (!_proto.TryIndex<EntityPrototype>(statusEffect, out var proto))
            return false;

        return proto.HasComponent<CENegativeStatusEffectComponent>(_compFactory);
    }

    private void OnAttemptStealMana(
        Entity<CEPacifismStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CEAttemptStealManaEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        // Sender is the entity being drained (victim). Block when the victim is a player.
        if (!HasComp<CEDungeonPlayerComponent>(args.Args.Sender))
            return;

        args.Args.Cancelled = true;
    }
}
