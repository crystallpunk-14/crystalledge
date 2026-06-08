using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Music;
using Content.Shared.CCVar;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CE.Music;

/// <summary>
/// Client-side system that plays two-layered ambient background music based on
/// <see cref="CEAmbientMusicPrototype"/>. Both layers always play simultaneously;
/// intensity controls which layers are audible:
///   0 — CalmLayer only
///   1 — CalmLayer + BattleLayer
/// Volumes are smoothly interpolated toward their targets each frame.
/// </summary>
public sealed partial class CEAmbientMusicSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private ISawmill _sawmill = default!;

    private const int LayerCount = 2;
    private const float FadeRate = 8f;
    private const float MinVolume = -32f;

    private ProtoId<CEAmbientMusicPrototype>? _currentProtoId;

    // [0] = Calm, [1] = Battle
    private readonly EntityUid?[] _streams = new EntityUid?[LayerCount];

    // Natural ("on") volume per layer = specifier.Params.Volume + _volumeSlider
    private readonly float[] _layerBaseVolumes = new float[LayerCount];

    // Volume each active layer is currently fading toward
    private readonly float[] _targetVolumes = { MinVolume, MinVolume };

    // Tracked current Params.Volume per layer (avoids reading from OpenAL each frame)
    private readonly float[] _currentVolumes = { MinVolume, MinVolume };

    private int _currentIntensity;
    private float _volumeSlider;

    // Threat intensity: set to 1 when local player has CEGOAPTargetComponent.
    // Stays at 1 for ThreatLingerSeconds after the component is removed.
    private const float ThreatLingerSeconds = 6f;
    private TimeSpan _threatExpireTime = TimeSpan.Zero;
    private bool _isThreatActive;

    // When true, the next Update tick will reset all stream positions to 0 so they stay phase-locked.
    // Deferred because OpenAL buffers may not be initialized in the same frame as PlayGlobal.
    private bool _needsSync;

    // Old streams queued to fade out before being stopped, with their tracked current volume
    private readonly List<EntityUid> _fadingOutStreams = new();
    private readonly Dictionary<EntityUid, float> _fadingOutVolumes = new();

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<CEMapAmbientMusicThemeComponent> _mapThemeQuery;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        _sawmill = _logManager.GetSawmill("audio.ce-ambient");
        _xformQuery = GetEntityQuery<TransformComponent>();
        _mapThemeQuery = GetEntityQuery<CEMapAmbientMusicThemeComponent>();
        Subs.CVar(_cfg, CCVars.AmbientMusicVolume, OnVolumeChanged, true);

        SubscribeLocalEvent<ActorComponent, EntParentChangedMessage>(OnParentChanged); //Prohibited dark magic used here! TODO: remove that cursed subscription
    }

    private void UpdateThreatIntensity()
    {
        var localPlayer = _player.LocalEntity;
        var isTargeted = localPlayer.HasValue && HasComp<CEGOAPTargetComponent>(localPlayer.Value);

        if (isTargeted)
        {
            _threatExpireTime = _timing.CurTime + TimeSpan.FromSeconds(ThreatLingerSeconds);
            if (!_isThreatActive)
            {
                _isThreatActive = true;
                if (_currentProtoId != null)
                    SetIntense(1);
            }
        }
        else if (_isThreatActive)
        {
            if (_timing.CurTime >= _threatExpireTime)
            {
                _isThreatActive = false;
                if (_currentProtoId != null)
                    SetIntense(0);
            }
        }
    }

    private void OnParentChanged(Entity<ActorComponent> ent, ref EntParentChangedMessage args)
    {
        if (args.Entity != _player.LocalEntity)
            return;

        var mapUid = _xformQuery.TryGetComponent(args.Entity, out var xform) ? xform.MapUid : null;
        if (mapUid == null || !_mapThemeQuery.TryGetComponent(mapUid.Value, out var theme) || theme.Theme == null)
        {
            StopMusic();
            return;
        }

        SetMusic(theme.Theme.Value);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        for (var i = 0; i < LayerCount; i++)
        {
            _audio.Stop(_streams[i]);
        }

        foreach (var stream in _fadingOutStreams)
        {
            _audio.Stop(stream);
        }

        _fadingOutVolumes.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        UpdateThreatIntensity();

        // On the first Update after SetMusic, all audio buffers are guaranteed initialized
        // reset all streams to position 0 so they stay phase-locked.
        if (_needsSync)
        {
            _needsSync = false;
            for (var i = 0; i < LayerCount; i++)
            {
                if (_streams[i] != null)
                {
                    _audio.SetPlaybackPosition(_streams[i], 0f);
                    if (TryComp<AudioComponent>(_streams[i]!.Value, out var sc))
                        _sawmill.Debug($"[CE-Ambient] Sync frame: Layer {i} pos after reset={sc.PlaybackPosition:F4}s");
                }
            }
        }

        for (var i = _fadingOutStreams.Count - 1; i >= 0; i--)
        {
            var stream = _fadingOutStreams[i];

            if (!TryComp<AudioComponent>(stream, out var comp))
            {
                _fadingOutStreams.RemoveAt(i);
                _fadingOutVolumes.Remove(stream);
                continue;
            }

            _fadingOutVolumes.TryGetValue(stream, out var trackedVol);
            var vol = trackedVol - FadeRate * frameTime;

            if (vol <= MinVolume)
            {
                _audio.Stop(stream);
                _fadingOutStreams.RemoveAt(i);
                _fadingOutVolumes.Remove(stream);
            }
            else
            {
                _fadingOutVolumes[stream] = vol;
                _audio.SetVolume(stream, vol, comp);
            }
        }

        // Smoothly move active layer volumes toward their targets
        for (var i = 0; i < LayerCount; i++)
        {
            if (_streams[i] == null)
                continue;

            if (!TryComp<AudioComponent>(_streams[i]!.Value, out var comp))
            {
                _streams[i] = null;
                continue;
            }

            var target = _targetVolumes[i];
            var current = _currentVolumes[i];

            if (MathF.Abs(current - target) < 0.05f)
                continue;

            var newVol = current + FadeRate * frameTime * MathF.Sign(target - current);
            newVol = target > current
                ? MathF.Min(newVol, target)
                : MathF.Max(newVol, target);
            newVol = MathF.Max(newVol, MinVolume); // defensive floor - prevents gain underflow to 0

            _currentVolumes[i] = newVol;
            _audio.SetVolume(_streams[i]!.Value, newVol, comp);
        }
    }

    /// <summary>
    /// Switches to a new ambient music prototype. Fades out the current track and fades in the new one,
    /// always starting at intensity 0 (CalmLayer only).
    /// </summary>
    public void SetMusic(ProtoId<CEAmbientMusicPrototype> protoId)
    {
        if (_currentProtoId == protoId)
            return;

        _sawmill.Debug($"Switching ambient music from '{_currentProtoId?.ToString() ?? "none"}' to '{protoId}'.");

        for (var i = 0; i < LayerCount; i++)
        {
            if (_streams[i] != null)
            {
                _fadingOutStreams.Add(_streams[i]!.Value);
                _fadingOutVolumes[_streams[i]!.Value] = _currentVolumes[i];
            }
        }

        for (var i = 0; i < LayerCount; i++)
            _streams[i] = null;

        _currentProtoId = protoId;
        _currentIntensity = 0;

        var proto = _proto.Index(protoId);
        SoundSpecifier?[] specifiers = [proto.CalmLayer, proto.BattleLayer];

        for (var i = 0; i < LayerCount; i++)
        {
            _targetVolumes[i] = MinVolume;
            _currentVolumes[i] = MinVolume;

            if (specifiers[i] == null)
            {
                _layerBaseVolumes[i] = MinVolume;
                continue;
            }

            _layerBaseVolumes[i] = specifiers[i]!.Params.Volume + _volumeSlider;
            // GainToVolume(0) = -Infinity; clamp so targets stay finite.
            if (!float.IsFinite(_layerBaseVolumes[i]))
                _layerBaseVolumes[i] = MinVolume;

            var result = _audio.PlayGlobal(
                specifiers[i]!,
                Filter.Local(),
                false,
                specifiers[i]!.Params.WithVolume(MinVolume).WithLoop(true));

            _streams[i] = result?.Entity;
        }

        // Apply intensity 0: CalmLayer fades in, BattleLayer stays muted
        ApplyIntensityTargets();

        // Force all streams to position 0 so they stay synchronized.
        // NOTE: buffers may not be ready this frame — _needsSync defers the actual
        // SetPlaybackPosition to the first Update() tick where OpenAL is guaranteed ready.
        _needsSync = true;
    }

    public void SetIntense(int intensity)
    {
        _sawmill.Debug($"Switching ambient music intensity from {_currentIntensity} to {intensity}.");

        _currentIntensity = Math.Clamp(intensity, 0, LayerCount - 1);
        ApplyIntensityTargets();
    }

    private void ApplyIntensityTargets()
    {
        for (var i = 0; i < LayerCount; i++)
        {
            _targetVolumes[i] = i <= _currentIntensity
                ? _layerBaseVolumes[i]
                : MinVolume;
        }
    }

    private void StopMusic()
    {
        if (_currentProtoId == null)
            return;

        _sawmill.Debug($"Stopping ambient music (was '{_currentProtoId}').");

        for (var i = 0; i < LayerCount; i++)
        {
            if (_streams[i] != null)
            {
                _fadingOutStreams.Add(_streams[i]!.Value);
                _fadingOutVolumes[_streams[i]!.Value] = _currentVolumes[i];
            }
        }

        for (var i = 0; i < LayerCount; i++)
            _streams[i] = null;

        _currentProtoId = null;
        _currentIntensity = 0;
    }

    private void OnVolumeChanged(float value)
    {
        _volumeSlider = SharedAudioSystem.GainToVolume(value);

        if (_currentProtoId == null)
            return;

        var proto = _proto.Index(_currentProtoId.Value);
        SoundSpecifier?[] specifiers = [proto.CalmLayer, proto.BattleLayer];

        for (var i = 0; i < LayerCount; i++)
        {
            if (specifiers[i] == null)
                continue;

            var rawVol = specifiers[i]!.Params.Volume + _volumeSlider;
            _layerBaseVolumes[i] = float.IsFinite(rawVol) ? rawVol : MinVolume;
        }

        ApplyIntensityTargets();
    }
}
