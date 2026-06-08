using Content.Shared._CE.TileEffects.Core;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CE.TileEffects.TileVFX;

public sealed partial class CETileEffectVFXSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const double SoundMergeWindow = 1.0;

    private readonly Dictionary<EntityCoordinates, TimeSpan> _recentSounds = new();

    private TimeSpan _nextCleanup;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CETileEffectVFXComponent, MapInitEvent>(OnStart);
        SubscribeLocalEvent<CETileEffectVFXComponent, CETileEffectStackEditedEvent>(OnEdited);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCleanup)
            return;

        _nextCleanup = _timing.CurTime + TimeSpan.FromSeconds(1.0);

        var toRemove = new List<EntityCoordinates>();
        foreach (var (coords, lastPlayed) in _recentSounds)
        {
            if (lastPlayed < _timing.CurTime - TimeSpan.FromSeconds(SoundMergeWindow))
                toRemove.Add(coords);
        }

        foreach (var coords in toRemove)
        {
            _recentSounds.Remove(coords);
        }
    }

    private void OnStart(Entity<CETileEffectVFXComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var coords = Transform(ent).Coordinates;

        if (ent.Comp.OnAppliedVfx is not null)
            Spawn(ent.Comp.OnAppliedVfx, coords);

        if (ent.Comp.OnAppliedSound is not null)
            TryPlayMergedSound(ent.Comp.OnAppliedSound, coords, ent.Comp.MergeSoundRange);
    }

    private void OnEdited(Entity<CETileEffectVFXComponent> ent, ref CETileEffectStackEditedEvent args)
    {
        if (_net.IsClient)
            return;

        var coords = Transform(ent).Coordinates;

        if (args.NewStack > args.OldStack)
        {
            if (ent.Comp.OnStacksAddedVfx is not null)
                Spawn(ent.Comp.OnStacksAddedVfx, coords);
            if (ent.Comp.OnStacksAddedSound is not null)
                TryPlayMergedSound(ent.Comp.OnStacksAddedSound, coords, ent.Comp.MergeSoundRange);
        }
        else if (args.NewStack < args.OldStack)
        {
            if (ent.Comp.OnStacksRemovedVfx is not null)
                Spawn(ent.Comp.OnStacksRemovedVfx, coords);
            if (ent.Comp.OnStacksRemovedSound is not null)
                TryPlayMergedSound(ent.Comp.OnStacksRemovedSound, coords, ent.Comp.MergeSoundRange);
        }
    }

    private void TryPlayMergedSound(SoundSpecifier sound, EntityCoordinates coords, float mergeRange)
    {
        var cutoff = _timing.CurTime - TimeSpan.FromSeconds(SoundMergeWindow);
        var mergeRangeSq = mergeRange * mergeRange;

        foreach (var (cached, lastPlayed) in _recentSounds)
        {
            if (lastPlayed < cutoff)
                continue;

            if ((cached.Position - coords.Position).LengthSquared() <= mergeRangeSq)
                return;
        }

        _recentSounds[coords] = _timing.CurTime;
        _audio.PlayPvs(sound, coords);
    }
}
