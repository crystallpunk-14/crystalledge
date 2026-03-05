using Content.Shared._CE.Actions.Components;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared.Actions.Events;

namespace Content.Shared._CE.Actions;

public abstract partial class CESharedActionSystem
{
    private void InitializePerformed()
    {
        SubscribeLocalEvent<CEActionManaCostComponent, ActionPerformedEvent>(OnManaCostActionPerformed);
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
            _magicEnergy.ChangeEnergy((args.Performer, playerMana), -manaCost, out _, out _);
    }
}
