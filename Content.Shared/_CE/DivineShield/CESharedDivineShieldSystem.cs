using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.DivineShield;

public abstract class CESharedDivineShieldSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly CEStatusEffectStackSystem _status = default!;

    private readonly EntProtoId _divineShieldProto = "CEStatusEffectDivineShield";

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

        var pos = Transform(ent).Coordinates;

        RaiseBreakEffect(status.AppliedTo, ent.Comp.BreakVfx, args.Args.Source);
        _audio.PlayPredicted(ent.Comp.BreakSound, pos, args.Args.Source);
        _status.TryRemoveStack(ent.Owner);

        RaiseLocalEvent(status.AppliedTo.Value, new CEDivineShieldBrokenEvent(status.AppliedTo.Value, ent.Comp.Applier, args.Args.Damage.Total, false));
        if (ent.Comp.Applier is not null)
            RaiseLocalEvent(ent.Comp.Applier.Value, new CEDivineShieldBrokenEvent(status.AppliedTo.Value, ent.Comp.Applier, args.Args.Damage.Total, true));
    }

    /// <summary>
    /// Spawns break VFX. Server sends a network event; client spawns locally during prediction.
    /// </summary>
    protected virtual void RaiseBreakEffect(EntityUid? ent, EntProtoId? breakVfx, EntityUid? source)
    {
    }

    public bool TryAddShield(EntityUid target, EntityUid? source, int stack = 1, TimeSpan? duration = null)
    {
        if (stack <= 0)
            return false;

        if (!_status.TryAddStack(target, _divineShieldProto, out var effectEntity, stack, duration))
            return false;

        if (TryComp<CEDivineShieldStatusEffectComponent>(effectEntity, out var statusEffect))
        {
            statusEffect.Applier = source;
            Dirty(effectEntity.Value, statusEffect);
        }

        return true;
    }
}

/// <summary>
/// Sent by the server to clients when a divine shield breaks, so they can spawn the VFX.
/// The predicting player spawns VFX locally and is excluded from this event.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEDivineShieldBreakEffectEvent(NetCoordinates coordinates, string? breakVfx) : EntityEventArgs
{
    public NetCoordinates Coordinates = coordinates;
    public string? BreakVfx = breakVfx;
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
