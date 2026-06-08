using Content.Shared._CE.Health;
using Content.Shared.Rejuvenate;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CE.DPSMeter;

/// <summary>
/// Shared system for <see cref="CEDPSMeterComponent"/>.
/// Subscribes to <see cref="CEDamageChangedEvent"/> to accumulate MaxDPS / TotalDamage
/// and resets the session after TrackTimeAfterHit + FadeDuration of silence.
/// Runs on both server and client; server state is authoritative via AutoGenerateComponentState.
/// </summary>
public sealed partial class CEDPSMeterSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _netMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDPSMeterComponent, CEDamageChangedEvent>(OnDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CEDPSMeterComponent>();
        while (query.MoveNext(out var uid, out var meter))
        {
            if (meter.LastHitTime == TimeSpan.Zero)
                continue;

            var resetTime = meter.LastHitTime + meter.TrackTimeAfterHit + meter.FadeDuration;
            if (_timing.CurTime < resetTime)
                continue;

            meter.TotalDamage = 0;
            meter.MaxDPS = 0f;
            meter.StartTrackTime = TimeSpan.Zero;
            meter.LastHitTime = TimeSpan.Zero;
            Dirty(uid, meter);

            // Raise rejuvenate on the server only to strip all debuffs from the target.
            if (_netMan.IsServer)
                RaiseLocalEvent(uid, new RejuvenateEvent());
        }
    }

    private void OnDamageChanged(Entity<CEDPSMeterComponent> ent, ref CEDamageChangedEvent args)
    {
        // Avoid double-processing during server-state application on the client.
        if (_timing.ApplyingState)
            return;

        if (!args.DamageIncreased || args.DamageDelta <= 0)
            return;

        ent.Comp.TotalDamage += args.DamageDelta;

        var now = _timing.CurTime;

        if (ent.Comp.StartTrackTime == TimeSpan.Zero)
            ent.Comp.StartTrackTime = now;

        ent.Comp.LastHitTime = now;

        var elapsed = (float)(now - ent.Comp.StartTrackTime).TotalSeconds;
        var currentDPS = ent.Comp.TotalDamage / MathF.Max(elapsed, 1f);

        if (currentDPS > ent.Comp.MaxDPS)
            ent.Comp.MaxDPS = currentDPS;

        Dirty(ent);
    }
}
