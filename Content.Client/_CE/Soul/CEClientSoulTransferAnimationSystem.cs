using System.Numerics;
using Content.Shared._CE.Soul.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._CE.Soul;

/// <summary>
/// Plays the visual animation for <see cref="CESoulTransferComponent"/> on the
/// client. Spawns one particle per soul spent, each with a randomised outward
/// distance and flight time.
/// Total animation duration is bounded by
/// <c>CESoulTransferComponent.Duration</c>; per-particle flight times are clamped
/// inside that budget so every particle arrives before the server removes the
/// component. A burst <c>CEEffectSoulCollect</c> is spawned at the player on
/// startup, and one more is spawned at the receiver each time a particle arrives.
/// </summary>
public sealed class CEClientSoulTransferAnimationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntProtoId ParticleProto = "CESoulTransferParticle";
    private EntProtoId CollectVfxProto = "CEEffectSoulCollect";

    /// <summary>
    /// Fraction of each particle's individual flight time spent bursting outward
    /// (the remainder is the inward convergence into the receiver).
    /// </summary>
    private const float OutwardPhase = 0.4f;

    private const float OutwardDistMin = 0.9f;
    private const float OutwardDistMax = 1.5f;

    /// <summary>
    /// Per-particle flight time as a fraction of the component's total Duration.
    /// Capped strictly below 1 so the particle always arrives before the server
    /// removes the component (which would trigger shutdown cleanup).
    /// </summary>
    private const float FlightTimeMin = 0.65f;
    private const float FlightTimeMax = 0.95f;

    private readonly Dictionary<EntityUid, ParticleState> _states = new();

    private sealed class ParticleData
    {
        public EntityUid Entity;
        public Vector2 Direction;
        public float OutwardDistance;
        public float FlightTime;
        public bool Arrived;
    }

    private sealed class ParticleState
    {
        public Vector2 Origin;
        public Vector2 ReceiverPos;
        public MapId MapId;
        public readonly List<ParticleData> Particles = new();
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CESoulTransferComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<CESoulTransferComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>
    /// State-driven initialisation: ComponentStartup fires on the client before
    /// AutoGenerateComponentState has populated the component fields, so Receiver/
    /// Cost/StartTime would all be default. Hooking AfterAutoHandleStateEvent
    /// guarantees we see the real values. We guard with _states.ContainsKey so
    /// later state updates (if any) don't restart the animation.
    /// </summary>
    private void OnAfterState(Entity<CESoulTransferComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        TryInit(ent);
    }

    private void TryInit(Entity<CESoulTransferComponent> ent)
    {
        if (_states.ContainsKey(ent.Owner))
            return;

        if (!TryGetEntity(ent.Comp.Receiver, out var receiverUid) || !Exists(receiverUid))
            return;

        var origin = _transform.GetWorldPosition(ent.Owner);
        var receiverPos = _transform.GetWorldPosition(receiverUid.Value);
        var mapId = Transform(ent.Owner).MapID;

        var state = new ParticleState
        {
            Origin = origin,
            ReceiverPos = receiverPos,
            MapId = mapId,
        };

        var spawnCoords = Transform(ent.Owner).Coordinates;

        // One burst VFX at the player center as the transfer begins.
        Spawn(CollectVfxProto, spawnCoords);

        var count = MathF.Max(1, ent.Comp.Cost);
        for (var i = 0; i < count; i++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            var particle = Spawn(ParticleProto, spawnCoords);
            // Force exact world center so particles don't drift on first frame.
            _transform.SetWorldPosition(particle, origin);

            state.Particles.Add(new ParticleData
            {
                Entity = particle,
                Direction = dir,
                OutwardDistance = _random.NextFloat(OutwardDistMin, OutwardDistMax),
                FlightTime = ent.Comp.Duration * _random.NextFloat(FlightTimeMin, FlightTimeMax),
                Arrived = false,
            });
        }

        _states[ent.Owner] = state;
    }

    private void OnShutdown(Entity<CESoulTransferComponent> ent, ref ComponentShutdown args)
    {
        if (!_states.Remove(ent.Owner, out var state))
            return;

        foreach (var p in state.Particles)
        {
            if (!p.Arrived && Exists(p.Entity))
                Del(p.Entity);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var (uid, state) in _states)
        {
            if (!TryComp<CESoulTransferComponent>(uid, out var comp))
                continue;

            var elapsed = (float) (_timing.CurTime - comp.StartTime).TotalSeconds;
            if (elapsed < 0f)
                elapsed = 0f;

            foreach (var p in state.Particles)
            {
                if (p.Arrived)
                    continue;

                if (!Exists(p.Entity))
                {
                    p.Arrived = true;
                    continue;
                }

                var t = elapsed / p.FlightTime;

                if (t >= 1f)
                {
                    // Arrival: burst VFX at receiver, despawn the particle.
                    Spawn(CollectVfxProto, new MapCoordinates(state.ReceiverPos, state.MapId));
                    Del(p.Entity);
                    p.Arrived = true;
                    continue;
                }

                Vector2 pos;
                if (t < OutwardPhase)
                {
                    // Outward burst with cubic ease-out.
                    var k = t / OutwardPhase;
                    var eased = 1f - MathF.Pow(1f - k, 3f);
                    pos = state.Origin + p.Direction * p.OutwardDistance * eased;
                }
                else
                {
                    // Converge to receiver with quadratic ease-in for snappy finish.
                    var outwardEnd = state.Origin + p.Direction * p.OutwardDistance;
                    var k = (t - OutwardPhase) / (1f - OutwardPhase);
                    var eased = k * k;
                    pos = Vector2.Lerp(outwardEnd, state.ReceiverPos, eased);
                }

                _transform.SetWorldPosition(p.Entity, pos);
            }
        }
    }
}
