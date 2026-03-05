using Content.Shared._CE.Actions;
using Content.Shared._CE.Actions.Components;
using Content.Shared.Actions.Events;

namespace Content.Server._CE.Actions;

public sealed partial class CEActionSystem
{
    private void InitializeDoAfter()
    {
        SubscribeLocalEvent<CEActionDoAfterVisualsComponent, CEActionStartDoAfterEvent>(OnSpawnMagicVisualEffect);
        SubscribeLocalEvent<CEActionDoAfterVisualsComponent, ActionDoAfterEvent>(OnDespawnMagicVisualEffect);
    }

    private void OnSpawnMagicVisualEffect(Entity<CEActionDoAfterVisualsComponent> ent, ref CEActionStartDoAfterEvent args)
    {
        QueueDel(ent.Comp.SpawnedEntity);

        var performer = GetEntity(args.Performer);
        var vfx = SpawnAttachedTo(ent.Comp.Proto, Transform(performer).Coordinates);
        _transform.SetParent(vfx, performer);
        ent.Comp.SpawnedEntity = vfx;
    }

    private void OnDespawnMagicVisualEffect(Entity<CEActionDoAfterVisualsComponent> ent, ref ActionDoAfterEvent args)
    {
        if (args.Repeat)
            return;

        QueueDel(ent.Comp.SpawnedEntity);
        ent.Comp.SpawnedEntity = null;
    }
}
