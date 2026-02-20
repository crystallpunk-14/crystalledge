using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared._CE.Stats.Core;
using Content.Shared._CE.Stats.Core.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.IntelligenceMaxMana;

/// <summary>
/// Handles linking Intelligence to maximum mana by updating the magic energy container capacity.
/// </summary>
public sealed partial class CEIntelligenceMaxManaSystem : EntitySystem
{
    [Dependency] private readonly CESharedMagicEnergySystem _magic = default!;

    private readonly ProtoId<CECharacterStatPrototype> _intelligenceStat = "Intelligence";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEIntelligenceMaxManaComponent, CEStatUpdatedEvent>(OnIntelligenceUpdated);
    }

    private void OnIntelligenceUpdated(Entity<CEIntelligenceMaxManaComponent> ent, ref CEStatUpdatedEvent args)
    {
        if (args.StatType != _intelligenceStat)
            return;

        if (!TryComp<CEMagicEnergyContainerComponent>(ent, out var container))
            return;

        var targetMax = (int)Math.Ceiling(ent.Comp.ManaPerIntelligence * args.NewValue);

        if (container.MaxEnergy == targetMax)
            return;

        _magic.SetMaximumEnergy(ent.Owner, targetMax);
    }
}
