using Content.Shared._CE.Conveyor;
using Content.Shared.Conveyor;

namespace Content.Server._CE.Conveyor;

public sealed class CEAutoConveyorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEAutoConveyorComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CEAutoConveyorComponent> ent, ref MapInitEvent args)
    {
        var conveyor = EnsureComp<ConveyorComponent>(ent);
        conveyor.State = ConveyorState.Forward;
        conveyor.Powered = true;
        Dirty(ent, conveyor);
    }
}
