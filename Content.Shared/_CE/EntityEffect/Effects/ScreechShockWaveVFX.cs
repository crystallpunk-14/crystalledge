using Content.Shared._CE.ShockWave;
using Robust.Shared.Network;
using Robust.Shared.Spawners;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class ScreechShockWaveVFX : CEEntityEffectBase<ScreechShockWaveVFX>
{
    [DataField]
    public float WaveSpeed = 15.3f;

    [DataField]
    public float WaveStrength = 1.08f;

    [DataField]
    public float DownScale  = 1.5f;

    /// <summary>
    /// Seconds
    /// </summary>
    [DataField]
    public float Time = 5;
}

public sealed partial class CEScreechShockWaveVFXEffectSystem : CEEntityEffectSystem<ScreechShockWaveVFX>
{
    [Dependency] private readonly INetManager _net = default!;

    protected override void Effect(ref CEEntityEffectEvent<ScreechShockWaveVFX> args)
    {
        if (!TryResolveEffectCoordinates(args.Args, args.Effect.EffectTarget, out var coords))
            return;

        if (_net.IsClient)
            return;

        var vfx = Spawn(null, coords);
        var shockWave = EnsureComp<CEScreechShockWaveComponent>(vfx);

        shockWave.WaveSpeed = args.Effect.WaveSpeed;
        shockWave.WaveStrength = args.Effect.WaveStrength;
        shockWave.DownScale = args.Effect.DownScale;
        Dirty(vfx, shockWave);

        var lifetime = EnsureComp<TimedDespawnComponent>(vfx);
        lifetime.Lifetime = args.Effect.Time;
        Dirty(vfx, lifetime);
    }
}
