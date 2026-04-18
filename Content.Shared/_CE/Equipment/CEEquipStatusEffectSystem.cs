using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Inventory.Events;

namespace Content.Shared._CE.Equipment;

/// <summary>
/// Applies / removes a status-effect stack on the wearer when equipment
/// with <see cref="CEEquipStatusEffectComponent"/> is equipped / unequipped.
/// </summary>
public sealed partial class CEEquipStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stacks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEquipStatusEffectComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CEEquipStatusEffectComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<CEEquipStatusEffectComponent> ent, ref GotEquippedEvent args)
    {
        _stacks.TryAddStack(args.Equipee, ent.Comp.StatusEffect, out _);
    }

    private void OnUnequipped(Entity<CEEquipStatusEffectComponent> ent, ref GotUnequippedEvent args)
    {
        _stacks.TryRemoveStack(args.Equipee, ent.Comp.StatusEffect);
    }
}
