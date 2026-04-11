using Content.Shared._CE.Health.Components;
using Content.Shared.Inventory;

namespace Content.Shared._CE.Health;

public sealed partial class CEClothingHealBonusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEClothingHealBonusComponent, InventoryRelayedEvent<CEGetIncomingHealEvent>>(OnInventoryHeal);
    }

    private void OnInventoryHeal(Entity<CEClothingHealBonusComponent> ent, ref InventoryRelayedEvent<CEGetIncomingHealEvent> args)
    {
        args.Args.HealAmount += ent.Comp.Flat;
    }
}
