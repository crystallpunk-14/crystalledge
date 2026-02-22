using Content.Shared._CE.Actions.Components;
using Content.Shared._CE.Actions.Events;
using Content.Shared.Actions.Events;
using Content.Shared.Power.Components;

namespace Content.Shared._CE.Actions;

public abstract partial class CESharedActionSystem
{
    private void InitializePerformed()
    {
        SubscribeLocalEvent<CEActionStaminaCostComponent, ActionPerformedEvent>(OnStaminaCostActionPerformed);
        SubscribeLocalEvent<CEActionManaCostComponent, ActionPerformedEvent>(OnManaCostActionPerformed);
    }

    private void OnStaminaCostActionPerformed(Entity<CEActionStaminaCostComponent> ent, ref ActionPerformedEvent args)
    {
        _stamina.TakeStaminaDamage(args.Performer, ent.Comp.Stamina, visual: false);
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

        //First - try to take mana from container
        if (!innate && TryComp<BatteryComponent>(action.Container, out var battery))
        {
            var spellEv = new CESpellFromSpellStorageUsedEvent(args.Performer, ent, manaCost);
            RaiseLocalEvent(action.Container.Value, ref spellEv);

            var energyTaken = MathF.Min(battery.LastCharge, (float)manaCost);

            _battery.ChangeCharge((action.Container.Value, battery), -(float)manaCost);
            manaCost -= energyTaken;
        }

        //Second - action user
        if (manaCost > 0 && TryComp<BatteryComponent>(args.Performer, out var playerMana))
            _battery.ChangeCharge((args.Performer, playerMana), -(float)manaCost);

        //And spawn mana trace
        //_magicVision.SpawnMagicTrace(
        //        Transform(args.Performer).Coordinates,
        //        action.Icon,
        //        Loc.GetString("ce-magic-vision-used-spell", ("name", MetaData(ent).EntityName)),
        //        TimeSpan.FromSeconds((float)ent.Comp.ManaCost * 50),
        //        args.Performer,
        //        null); //TODO: We need a way to pass spell target here
    }
}
