using Content.Shared._CE.Actions.Components;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.Components;
using Content.Shared.SSDIndicator;

namespace Content.Shared._CE.Actions;

public abstract partial class CESharedActionSystem
{
    private void InitializeAttempts()
    {

        SubscribeLocalEvent<ActionComponent, ActionAttemptEvent>(OnMobStateAttempt);
        SubscribeLocalEvent<CEActionFreeHandsRequiredComponent, ActionAttemptEvent>(OnSomaticActionAttempt);
        SubscribeLocalEvent<CEActionManaCostComponent, ActionAttemptEvent>(OnManacostActionAttempt);
        SubscribeLocalEvent<CEActionStaminaCostComponent, ActionAttemptEvent>(OnStaminaCostActionAttempt);
        SubscribeLocalEvent<CEActionDangerousComponent, ActionAttemptEvent>(OnDangerousActionAttempt);
        SubscribeLocalEvent<CEActionWeaponRequiredComponent, ActionAttemptEvent>(OnWeaponRequiredActionAttempt);

        SubscribeLocalEvent<CEActionSSDBlockComponent, ActionValidateEvent>(OnActionSSDAttempt);
        SubscribeLocalEvent<CEActionTargetMobStatusRequiredComponent, ActionValidateEvent>(OnTargetMobStatusRequiredValidate);
    }


    private void OnMobStateAttempt(Entity<ActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (!TryComp<CEMobStateComponent>(args.User, out var mobState))
            return;

        if (mobState.CurrentState == CEMobState.Alive)
            return;

        if (HasComp<CEActionCastableFromCriticalComponent>(ent))
            return;

        args.Cancelled = true;
    }

    /// <summary>
    /// Before using a spell, a mana check is made for the amount of mana to show warnings.
    /// </summary>
    private void OnManacostActionAttempt(Entity<CEActionManaCostComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ActionComponent>(ent, out var action))
            return;

        //Total mana required
        var requiredMana = ent.Comp.ManaCost;

        if (ent.Comp.CanModifyManacost)
        {
            var manaEv = new CECalculateManacostEvent(args.User, ent.Comp.ManaCost);

            RaiseLocalEvent(args.User, manaEv);

            if (action.Container is not null)
                RaiseLocalEvent(action.Container.Value, manaEv);

            requiredMana = manaEv.TotalManacost;
        }

        //Trying get mana from performer
        if (!TryComp<CEMagicEnergyContainerComponent>(args.User, out var playerMana))
        {
            Popup.PopupClient(Loc.GetString("ce-magic-spell-no-mana-component"), args.User, args.User);
            args.Cancelled = true;
            return;
        }

        if (playerMana.Energy < requiredMana)
        {
            Popup.PopupClient(Loc.GetString("ce-magic-spell-not-enough-mana"), args.User, args.User);
            args.Cancelled = true;
        }
    }

    private void OnStaminaCostActionAttempt(Entity<CEActionStaminaCostComponent> ent, ref ActionAttemptEvent args)
    {
        if (!TryComp<StaminaComponent>(args.User, out var staminaComp))
            return;

        if (!staminaComp.Critical)
            return;

        Popup.PopupClient(Loc.GetString("ce-magic-spell-stamina-not-enough"), args.User, args.User);
        args.Cancelled = true;
    }

    private void OnSomaticActionAttempt(Entity<CEActionFreeHandsRequiredComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryComp<HandsComponent>(args.User, out var hands) &&
            _hand.CountFreeHands((args.User, hands)) >= ent.Comp.FreeHandRequired)
            return;

        Popup.PopupClient(Loc.GetString("ce-magic-spell-need-somatic-component"), args.User, args.User);
        args.Cancelled = true;
    }

    private void OnDangerousActionAttempt(Entity<CEActionDangerousComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (HasComp<PacifiedComponent>(args.User))
        {
            Popup.PopupClient(Loc.GetString("ce-magic-spell-pacified"), args.User, args.User);
            args.Cancelled = true;
        }
    }

    private void OnWeaponRequiredActionAttempt(Entity<CEActionWeaponRequiredComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (_hand.TryGetActiveItem(args.User, out var held) &&
            HasComp<CEWeaponComponent>(held))
            return;

        Popup.PopupClient(Loc.GetString("ce-magic-weapon-required"), args.User, args.User);
        args.Cancelled = true;
    }

    private void OnTargetMobStatusRequiredValidate(Entity<CEActionTargetMobStatusRequiredComponent> ent,
        ref ActionValidateEvent args)
    {
        if (args.Invalid)
            return;

        var target = GetEntity(args.Input.EntityTarget);

        if (!TryComp<CEMobStateComponent>(target, out var mobStateComp))
        {
            Popup.PopupClient(Loc.GetString("ce-magic-spell-target-not-mob"), args.User, args.User);
            args.Invalid = true;
            return;
        }

        if (!ent.Comp.AllowedStates.Contains(mobStateComp.CurrentState))
        {
            var states = "";
            foreach (var state in ent.Comp.AllowedStates)
            {
                if (states.Length > 0)
                    states += ", ";

                if (state == CEMobState.Alive)
                    states += Loc.GetString("ce-magic-spell-target-mob-state-live");
                else if (state == CEMobState.Critical)
                    states += Loc.GetString("ce-magic-spell-target-mob-state-critical");
            }

            Popup.PopupClient(Loc.GetString("ce-magic-spell-target-mob-state", ("state", states)),
                args.User,
                args.User);
            args.Invalid = true;
        }
    }

    private void OnActionSSDAttempt(Entity<CEActionSSDBlockComponent> ent, ref ActionValidateEvent args)
    {
        if (args.Invalid)
            return;

        if (!TryComp<SSDIndicatorComponent>(GetEntity(args.Input.EntityTarget), out var ssdIndication))
            return;

        if (ssdIndication.IsSSD)
        {
            Popup.PopupClient(Loc.GetString("ce-magic-spell-ssd"), args.User, args.User);
            args.Invalid = true;
        }
    }
}
