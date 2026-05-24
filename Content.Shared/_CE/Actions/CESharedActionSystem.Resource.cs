using Content.Shared._CE.Actions.Components;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared._CE.Soul.Components;
using Content.Shared._CE.Stamina;
using Content.Shared.Actions.Events;

namespace Content.Shared._CE.Actions;

public abstract partial class CESharedActionSystem
{
    private void InitializePerformed()
    {
        SubscribeLocalEvent<CEActionManaCostComponent, ActionPerformedEvent>(OnManaCostActionPerformed);
        SubscribeLocalEvent<CEActionSoulCostComponent, ActionPerformedEvent>(OnSoulCostActionPerformed);
        SubscribeLocalEvent<CEActionStaminaCostComponent, ActionPerformedEvent>(OnStaminaCostActionPerformed);
    }

    private void OnManaCostActionPerformed(Entity<CEActionManaCostComponent> ent, ref ActionPerformedEvent args)
    {
        if (!_actionQuery.TryComp(ent, out var action))
            return;

        if (action.Container is null)
            return;

        var innate = action.Container == args.Performer;

        var manaCost = ent.Comp.ManaCost;

        if (ent.Comp.CanModifyManacost)
        {
            var manaEv = new CECalculateManacostEvent(args.Performer, ent.Comp.ManaCost);

            RaiseLocalEvent(args.Performer, manaEv);

            if (!innate)
                RaiseLocalEvent(action.Container.Value, manaEv);

            manaCost = manaEv.TotalManacost;
        }

        if (manaCost > 0 && TryComp<CEMagicEnergyContainerComponent>(args.Performer, out var playerMana))
            _magicEnergy.Take((args.Performer, playerMana), manaCost);
    }

    private void OnSoulCostActionPerformed(Entity<CEActionSoulCostComponent> ent, ref ActionPerformedEvent args)
    {
        if (!_actionQuery.TryComp(ent, out var action))
            return;

        if (action.Container is null)
            return;

        if (!TryComp<CESoulContainerComponent>(args.Performer, out var playerSoul))
            return;

        _soul.TryRemoveSouls((args.Performer, playerSoul), ent.Comp.Cost);
    }

    private void OnStaminaCostActionPerformed(Entity<CEActionStaminaCostComponent> ent, ref ActionPerformedEvent args)
    {
        _stamina.TryTakeDamage(args.Performer, ent.Comp.Cost);
    }
}
