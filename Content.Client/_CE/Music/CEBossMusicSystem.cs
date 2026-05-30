using Content.Shared._CE.Music;
using Content.Shared.CCVar;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CE.Music;

/// <summary>
/// Client-side player for the boss encounter soundtrack defined by <see cref="CEBossMusicPrototype"/>.
/// Watches the local player's current map for <see cref="CEMapBossMusicComponent"/> and drives playback
/// based on its networked <see cref="CEMapBossMusicComponent.State"/>:
///   Prelude — loops the prelude track
///   Battle  — plays the intro one-shot, then starts the main loop after <see cref="CEBossMusicPrototype.MainStartDelay"/>
///   Victory — stops the main loop immediately and plays the victory one-shot
/// </summary>
public sealed class CEBossMusicSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private const float MinVolume = -32f;

    private ISawmill _sawmill = default!;

    private ProtoId<CEBossMusicPrototype>? _currentProtoId;
    private CEBossMusicState _appliedState;
    private bool _hasMusic;

    private EntityUid? _preludeStream;
    private EntityUid? _introStream;
    private EntityUid? _mainStream;
    private EntityUid? _victoryStream;

    private bool _mainStartPending;
    private TimeSpan _mainStartTime;

    private float _volumeSlider;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<CEMapBossMusicComponent> _mapQuery;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        _sawmill = _logManager.GetSawmill("audio.ce-boss");
        _xformQuery = GetEntityQuery<TransformComponent>();
        _mapQuery = GetEntityQuery<CEMapBossMusicComponent>();

        Subs.CVar(_cfg, CCVars.AmbientMusicVolume, OnVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        StopAll();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        ResolveDesired(out var desiredProto, out var desiredState);

        if (desiredProto == null)
        {
            if (_hasMusic)
            {
                _sawmill.Debug("Local player left boss map; stopping boss music.");
                StopAll();
                _hasMusic = false;
                _currentProtoId = null;
            }
        }
        else if (!_hasMusic || _currentProtoId != desiredProto)
        {
            _sawmill.Debug($"Switching boss music to '{desiredProto.Value}' (state={desiredState}).");
            StopAll();
            _currentProtoId = desiredProto;
            _hasMusic = true;
            _appliedState = desiredState;
            ApplyState(desiredState);
        }
        else if (_appliedState != desiredState)
        {
            _sawmill.Debug($"Boss music state {_appliedState} -> {desiredState}.");
            _appliedState = desiredState;
            ApplyState(desiredState);
        }

        if (_mainStartPending && _timing.CurTime >= _mainStartTime)
        {
            _mainStartPending = false;
            StartMainLoop();
        }
    }

    private void ResolveDesired(out ProtoId<CEBossMusicPrototype>? proto, out CEBossMusicState state)
    {
        proto = null;
        state = CEBossMusicState.Prelude;

        var localPlayer = _player.LocalEntity;
        if (localPlayer == null)
            return;

        if (!_xformQuery.TryGetComponent(localPlayer.Value, out var xform))
            return;

        var mapUid = xform.MapUid;
        if (mapUid == null)
            return;

        if (!_mapQuery.TryGetComponent(mapUid.Value, out var comp))
            return;

        if (comp.Music == null)
            return;

        proto = comp.Music;
        state = comp.State;
    }

    private void ApplyState(CEBossMusicState state)
    {
        if (_currentProtoId == null)
            return;

        var proto = _proto.Index(_currentProtoId.Value);

        switch (state)
        {
            case CEBossMusicState.Prelude:
                StopStream(ref _introStream);
                StopStream(ref _mainStream);
                StopStream(ref _victoryStream);
                _mainStartPending = false;
                if (_preludeStream == null && proto.Prelude != null)
                    _preludeStream = PlayLoop(proto.Prelude);
                break;

            case CEBossMusicState.Battle:
                StopStream(ref _preludeStream);
                StopStream(ref _victoryStream);
                if (_introStream == null && proto.Intro != null)
                    _introStream = PlayOneShot(proto.Intro);
                if (_mainStream == null)
                {
                    _mainStartPending = true;
                    _mainStartTime = _timing.CurTime + TimeSpan.FromSeconds(proto.MainStartDelay);
                }
                break;

            case CEBossMusicState.Victory:
                StopStream(ref _preludeStream);
                StopStream(ref _introStream);
                StopStream(ref _mainStream);
                _mainStartPending = false;
                if (_victoryStream == null && proto.Victory != null)
                    _victoryStream = PlayOneShot(proto.Victory);
                break;
        }
    }

    private void StartMainLoop()
    {
        if (_currentProtoId == null || _appliedState != CEBossMusicState.Battle)
            return;

        var proto = _proto.Index(_currentProtoId.Value);
        if (proto.Main == null)
            return;

        _mainStream = PlayLoop(proto.Main);
    }

    private EntityUid? PlayLoop(SoundSpecifier spec)
    {
        var vol = ResolveVolume(spec);
        var result = _audio.PlayGlobal(spec, Filter.Local(), false,
            spec.Params.WithVolume(vol).WithLoop(true));
        return result?.Entity;
    }

    private EntityUid? PlayOneShot(SoundSpecifier spec)
    {
        var vol = ResolveVolume(spec);
        var result = _audio.PlayGlobal(spec, Filter.Local(), false,
            spec.Params.WithVolume(vol).WithLoop(false));
        return result?.Entity;
    }

    private float ResolveVolume(SoundSpecifier spec)
    {
        var vol = spec.Params.Volume + _volumeSlider;
        return float.IsFinite(vol) ? vol : MinVolume;
    }

    private void StopStream(ref EntityUid? stream)
    {
        if (stream == null)
            return;
        _audio.Stop(stream);
        stream = null;
    }

    private void StopAll()
    {
        StopStream(ref _preludeStream);
        StopStream(ref _introStream);
        StopStream(ref _mainStream);
        StopStream(ref _victoryStream);
        _mainStartPending = false;
        _appliedState = CEBossMusicState.Prelude;
    }

    private void OnVolumeChanged(float value)
    {
        _volumeSlider = SharedAudioSystem.GainToVolume(value);

        if (_currentProtoId == null)
            return;

        var proto = _proto.Index(_currentProtoId.Value);
        UpdateStreamVolume(_preludeStream, proto.Prelude);
        UpdateStreamVolume(_introStream, proto.Intro);
        UpdateStreamVolume(_mainStream, proto.Main);
        UpdateStreamVolume(_victoryStream, proto.Victory);
    }

    private void UpdateStreamVolume(EntityUid? stream, SoundSpecifier? spec)
    {
        if (stream == null || spec == null)
            return;
        _audio.SetVolume(stream, ResolveVolume(spec));
    }
}
