using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared.Cargo;
using Robust.Shared.Timing;

namespace Content.Server._CE.Mana;

public sealed partial class CEMagicEnergySystem : CESharedMagicEnergySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMagicEnergyContainerComponent, PriceCalculationEvent>(OnMagicEnergyPriceCalculation);
    }

    private void OnMagicEnergyPriceCalculation(Entity<CEMagicEnergyContainerComponent> ent, ref PriceCalculationEvent args)
    {
        args.Price += ent.Comp.Energy * 0.1f;
    }
}
