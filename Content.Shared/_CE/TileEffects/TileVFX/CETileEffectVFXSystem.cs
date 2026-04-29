using Content.Shared._CE.TileEffects.Core;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._CE.TileEffects;

public sealed class CETileEffectVFXSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CETileEffectVFXComponent, MapInitEvent>(OnStart);
        SubscribeLocalEvent<CETileEffectVFXComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CETileEffectVFXComponent, CETileEffectStackEditedEvent>(OnEdited);
    }

    private void OnStart(Entity<CETileEffectVFXComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var pos = Transform(ent).Coordinates;
        Spawn(ent.Comp.OnAppliedVfx, pos);

        _audio.PlayPvs(ent.Comp.OnAppliedSound, pos);
    }

    private void OnShutdown(Entity<CETileEffectVFXComponent> ent, ref ComponentShutdown args)
    {
        //if (_net.IsClient) //TODO: fix spawning on terminating entity
        //    return;
//
        //var mapPos = Transform(ent).Coordinates;
        //Spawn(ent.Comp.OnRemovedVfx, mapPos);
//
        //_audio.PlayPvs(ent.Comp.OnRemovedSound, mapPos);
    }

    private void OnEdited(Entity<CETileEffectVFXComponent> ent, ref CETileEffectStackEditedEvent args)
    {
        if (_net.IsClient)
            return;

        var pos = Transform(ent).Coordinates;

        if (args.NewStack > args.OldStack)
        {
            Spawn(ent.Comp.OnStacksAddedVfx, pos);
            _audio.PlayPvs(ent.Comp.OnStacksAddedSound, pos);
        }
        else if (args.NewStack < args.OldStack)
        {
            Spawn(ent.Comp.OnStacksRemovedVfx, pos);
            _audio.PlayPvs(ent.Comp.OnStacksRemovedSound, pos);
        }
    }
}
