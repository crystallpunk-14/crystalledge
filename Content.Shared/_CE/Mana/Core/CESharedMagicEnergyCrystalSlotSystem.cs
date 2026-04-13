using System.Diagnostics.CodeAnalysis;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Shared._CE.Mana.Core;

public abstract class CESharedMagicEnergyCrystalSlotSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly CESharedMagicEnergySystem _magicEnergy = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMagicEnergyCrystalSlotComponent, EntInsertedIntoContainerMessage>(OnCrystalInserted);
        SubscribeLocalEvent<CEMagicEnergyCrystalSlotComponent, EntRemovedFromContainerMessage>(OnCrystalRemoved);

        SubscribeLocalEvent<CEMagicEnergyCrystalComponent, CEMagicEnergyLevelChangeEvent>(OnEnergyChanged);

        SubscribeLocalEvent<CEMagicEnergyCrystalSlotComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CEMagicEnergyCrystalSlotComponent, CESlotCrystalChangedEvent>(OnCrystalChanged);
    }

    private void OnCrystalRemoved(Entity<CEMagicEnergyCrystalSlotComponent> slot, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != slot.Comp.SlotId)
            return;

        _appearance.SetData(slot, CEMagicSlotVisuals.Inserted, false);
        _appearance.SetData(slot, CEMagicSlotVisuals.Powered, false);
        RaiseLocalEvent(slot, new CESlotCrystalChangedEvent(true));
    }

    private void OnCrystalInserted(Entity<CEMagicEnergyCrystalSlotComponent> slot, ref EntInsertedIntoContainerMessage args)
    {
        if (!slot.Comp.Initialized)
            return;

        if (args.Container.ID != slot.Comp.SlotId)
            return;

        _appearance.SetData(slot, CEMagicSlotVisuals.Inserted, true);
        RaiseLocalEvent(slot, new CESlotCrystalChangedEvent(false));
    }

    public bool TryGetEnergyCrystalFromSlot(Entity<CEMagicEnergyCrystalSlotComponent?> ent,
        [NotNullWhen(true)] out Entity<CEMagicEnergyContainerComponent>? energyEnt)
    {
        energyEnt = null;

        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!_itemSlots.TryGetSlot(ent, ent.Comp.SlotId, out var slot))
            return false;

        if (slot.Item is null)
            return false;

        if (!TryComp<CEMagicEnergyContainerComponent>(slot.Item, out var energyComp))
            return false;

        energyEnt = (slot.Item.Value, energyComp);
        return true;
    }
    public bool HasEnergy(Entity<CEMagicEnergyCrystalSlotComponent?> ent,
        int energy,
        EntityUid? user = null)
    {
        if (!TryGetEnergyCrystalFromSlot(ent, out var energyEnt))
        {
            if (user is not null)
                _popup.PopupEntity(Loc.GetString("ce-magic-energy-no-crystal"), ent, user.Value);

            return false;
        }

        if (energyEnt.Value.Comp.Energy >= energy)
            return true;

        if (user is not null)
            _popup.PopupEntity(Loc.GetString("ce-magic-energy-insufficient"), ent, user.Value);

        return false;
    }

    //public bool TryChangeEnergy(Entity<CEMagicEnergyCrystalSlotComponent?> ent,
    //    int energy,
    //    EntityUid? user = null)
    //{
    //    if (!TryGetEnergyCrystalFromSlot(ent, out var energyEnt))
    //    {
    //        if (user is not null)
    //            _popup.PopupEntity(Loc.GetString("ce-magic-energy-no-crystal"), ent, user.Value);
//
    //        return false;
    //    }
//
    //    _magicEnergy.ChangeEnergy((energyEnt.Value, energyEnt.Value), energy, out _, out _);
    //    return true;
    //}

    private void OnCrystalChanged(Entity<CEMagicEnergyCrystalSlotComponent> ent, ref CESlotCrystalChangedEvent args)
    {
        var realPowered = TryGetEnergyCrystalFromSlot((ent, ent), out var energyComp);
        if (energyComp is not null)
            realPowered = energyComp.Value.Comp.Energy > 0;

        if (ent.Comp.Powered == realPowered)
            return;

        ent.Comp.Powered = realPowered;
        _appearance.SetData(ent, CEMagicSlotVisuals.Powered, realPowered);

        RaiseLocalEvent(ent, new CESlotCrystalPowerChangedEvent(realPowered));
    }

    private void OnEnergyChanged(Entity<CEMagicEnergyCrystalComponent> crystal, ref CEMagicEnergyLevelChangeEvent args)
    {
        if (!_container.TryGetContainingContainer((crystal.Owner, null, null), out var container))
            return;

        if (!TryComp(container.Owner, out CEMagicEnergyCrystalSlotComponent? slot))
            return;

        if (!_itemSlots.TryGetSlot(container.Owner, slot.SlotId, out var itemSlot))
            return;

        if (itemSlot.Item != crystal)
            return;

        RaiseLocalEvent(container.Owner, new CESlotCrystalChangedEvent(false));
    }

    private void OnExamined(Entity<CEMagicEnergyCrystalSlotComponent> ent, ref ExaminedEvent args)
    {
        if (!TryGetEnergyCrystalFromSlot((ent, ent), out var energyEnt))
            return;

        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(_magicEnergy.GetEnergyExaminedText((energyEnt.Value, energyEnt)));
    }
}
