using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.StatusEffectVFX;

public abstract class CESharedStatusEffectVFXSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStatusEffectVFXComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<CEStatusEffectVFXComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEStatusEffectVFXComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
    }

    private void OnApplied(Entity<CEStatusEffectVFXComponent> ent, ref StatusEffectAppliedEvent args)
    {
        var source = GetSource(ent);
        var pos = Transform(args.Target).Coordinates;
        PlayEffect(args.Target, source, ent.Comp.OnAppliedVfx, pos);
        _audio.PlayPredicted(ent.Comp.OnAppliedSound, pos, source);
    }

    private void OnRemoved(Entity<CEStatusEffectVFXComponent> ent, ref StatusEffectRemovedEvent args)
    {
        var source = GetSource(ent);
        var pos = Transform(args.Target).Coordinates;
        PlayEffect(args.Target, source, ent.Comp.OnRemovedVfx, pos);
        _audio.PlayPredicted(ent.Comp.OnRemovedSound, pos, source);
    }

    private void OnStackEdited(Entity<CEStatusEffectVFXComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        var source = GetSource(ent);
        var pos = Transform(args.Target).Coordinates;

        if (args.newStack > args.oldStack)
        {
            PlayEffect(args.Target, source, ent.Comp.OnStacksAddedVfx, pos);
            _audio.PlayPredicted(ent.Comp.OnStacksAddedSound, pos, source);
        }
        else if (args.newStack < args.oldStack)
        {
            PlayEffect(args.Target, source, ent.Comp.OnStacksRemovedVfx, pos);
            _audio.PlayPredicted(ent.Comp.OnStacksRemovedSound, pos, source);
        }
    }

    private EntityUid? GetSource(EntityUid effectEntity)
    {
        if (!TryComp<CEStatusEffectSourceComponent>(effectEntity, out var src))
            return null;
        return src.Source is { } s && Exists(s) ? s : null;
    }

    /// <summary>
    /// Spawns VFX with prediction support.
    /// Client spawns locally during prediction; server broadcasts to non-predicting clients.
    /// </summary>
    protected virtual void PlayEffect(EntityUid target, EntityUid? source, EntProtoId? vfx, EntityCoordinates pos)
    {
    }
}

/// <summary>
/// Network event sent by the server to spawn VFX on clients.
/// The predicting player is excluded since they spawn VFX locally.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEStatusEffectVFXEvent(NetCoordinates coordinates, string? vfx) : EntityEventArgs
{
    public NetCoordinates Coordinates = coordinates;
    public string? Vfx = vfx;
}
