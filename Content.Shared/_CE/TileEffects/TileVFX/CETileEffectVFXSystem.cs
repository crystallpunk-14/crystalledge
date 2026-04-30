using Content.Shared._CE.TileEffects.Core;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._CE.TileEffects.TileVFX;

public sealed class CETileEffectVFXSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CETileEffectVFXComponent, MapInitEvent>(OnStart);
        SubscribeLocalEvent<CETileEffectVFXComponent, CETileEffectStackEditedEvent>(OnEdited);
    }

    private void OnStart(Entity<CETileEffectVFXComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var pos = Transform(ent).Coordinates;
        if (ent.Comp.OnAppliedVfx is not null)
            Spawn(ent.Comp.OnAppliedVfx, pos);

        if (ent.Comp.OnAppliedSound is not null)
            _audio.PlayPvs(ent.Comp.OnAppliedSound, pos);
    }

    private void OnEdited(Entity<CETileEffectVFXComponent> ent, ref CETileEffectStackEditedEvent args)
    {
        if (_net.IsClient)
            return;

        var pos = Transform(ent).Coordinates;

        if (args.NewStack > args.OldStack)
        {
            if (ent.Comp.OnStacksAddedVfx is not null)
                Spawn(ent.Comp.OnStacksAddedVfx, pos);
            if (ent.Comp.OnStacksAddedSound is not null)
                _audio.PlayPvs(ent.Comp.OnStacksAddedSound, pos);
        }
        else if (args.NewStack < args.OldStack)
        {
            if (ent.Comp.OnStacksRemovedVfx is not null)
                Spawn(ent.Comp.OnStacksRemovedVfx, pos);
            if (ent.Comp.OnStacksRemovedSound is not null)
                _audio.PlayPvs(ent.Comp.OnStacksRemovedSound, pos);
        }
    }
}
