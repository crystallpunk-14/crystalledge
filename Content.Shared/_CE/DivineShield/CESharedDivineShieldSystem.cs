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
        BreakShield(ent, args.Args.Source);
    }

    private void BreakShield(Entity<CEDivineShieldStatusEffectComponent> ent, EntityUid? source)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var status))
            return;

        var pos = Transform(ent).Coordinates;

        RaiseBreakEffect(status.AppliedTo, ent.Comp.BreakVfx, source);
        _audio.PlayPredicted(ent.Comp.BreakSound, pos, source);

        _status.TryRemoveStack(ent.Owner);
    }

    /// <summary>
    /// Spawns break VFX. Server sends a network event; client spawns locally during prediction.
    /// </summary>
    protected virtual void RaiseBreakEffect(EntityUid? ent, EntProtoId? breakVfx, EntityUid? source)
    {
    }

    public bool TryAddShield(EntityUid target, int stack = 1, TimeSpan? duration = null)
    {
        if (stack <= 0)
            return false;

        if (!_status.TryAddStack(target, _divineShieldProto, stack, duration))
            return false;

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
