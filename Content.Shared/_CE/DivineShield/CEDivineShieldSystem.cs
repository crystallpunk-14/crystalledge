using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CE.DivineShield;

public sealed class CEDivineShieldSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly CEStatusEffectStackSystem _status = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly EntProtoId _divineShieldProto = "CEStatusEffectDivineShield";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDivineShieldStatusEffectComponent, StatusEffectRelayedEvent<CEDamageCalculateEvent>>(OnBeforeDamage);
    }

    private void OnBeforeDamage(Entity<CEDivineShieldStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEDamageCalculateEvent> args)
    {
        args.Args.Cancelled = true;
        BreakShield(ent);
    }

    private void BreakShield(Entity<CEDivineShieldStatusEffectComponent> ent)
    {
        if (_timing.IsFirstTimePredicted && _net.IsClient)
        {
            var pos = Transform(ent).Coordinates;
            SpawnAtPosition(ent.Comp.BreakVfx, pos);
            _audio.PlayPvs(ent.Comp.BreakSound, pos);
        }

        _status.TryRemoveStack(ent.Owner);
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
