using Content.Shared._CE.Fire;
using Content.Shared._CE.Water;
using Content.Shared.Conveyor;
using Robust.Shared.Physics.Events;

namespace Content.Server._CE.Water;

public sealed class CEWaterSystem : CESharedWaterSystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Conveyor activation for flowing water.
        SubscribeLocalEvent<CEWaterComponent, MapInitEvent>(OnMapInit);

        // Fire interaction: extinguish burning entities entering water.
        // Water interaction: apply wet stacks to entities entering water.
        SubscribeLocalEvent<CEWaterComponent, StartCollideEvent>(OnCollide);
    }

    private void OnMapInit(EntityUid uid, CEWaterComponent component, MapInitEvent args)
    {
        if (!component.Flowing)
            return;

        var conveyor = EnsureComp<ConveyorComponent>(uid);
        conveyor.State = ConveyorState.Forward;
        conveyor.Powered = true;
        Dirty(uid, conveyor);
    }

    /// <summary>
    /// Extinguish burning entities that touch water and apply wet stacks.
    /// </summary>
    private void OnCollide(Entity<CEWaterComponent> ent, ref StartCollideEvent args)
    {
        Fire.ExtinguishEntity(new Entity<CEFlammableComponent?>(args.OtherEntity, null));
        WetEntity(args.OtherEntity, maxStack: 10);
    }
}
