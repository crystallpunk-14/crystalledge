using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.DivineShield;

public sealed class CESharedDivineShieldSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDivineShieldStatusEffectComponent, StatusEffectRelayedEvent<CEDamageCalculateEvent>>(OnBeforeDamage);
    }

    private void OnBeforeDamage(Entity<CEDivineShieldStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEDamageCalculateEvent> args)
    {
        args.Args.Cancelled = true;

        if (!TryComp<StatusEffectComponent>(ent, out var status))
            return;

        if (status.AppliedTo is null)
            return;

        TryComp<CEStatusEffectSourceComponent>(ent, out var sourceComp);
        var applier = sourceComp?.Source is { } s && Exists(s) ? s : (EntityUid?) null;

        _status.TryRemoveStack(ent.Owner);

        RaiseLocalEvent(status.AppliedTo.Value, new CEDivineShieldBrokenEvent(status.AppliedTo.Value, applier, args.Args.Damage.Total, false));
        if (applier is not null)
            RaiseLocalEvent(applier.Value, new CEDivineShieldBrokenEvent(status.AppliedTo.Value, applier, args.Args.Damage.Total, true));
    }
}

/// <summary>
/// This effect triggers on two entities: the one whose divine shield has broken, and the one who cast the shield.
/// It is also propagated via Relay to all status effects on both entities.
/// Use this to apply special effects when the shield breaks.
/// </summary>
public sealed class CEDivineShieldBrokenEvent(EntityUid shieldHolder, EntityUid? applier, int damageConsumed, bool raisedOnApplier) : EntityEventArgs
{
    public EntityUid ShieldHolder = shieldHolder;
    public EntityUid? Applier = applier;
    public int DamageConsumed = damageConsumed;
    public bool RaisedOnApplier = raisedOnApplier;
}
