using Content.Shared._CE.Actions.Components;
using Content.Shared._CE.Health.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Examine;

namespace Content.Shared._CE.Actions;

public abstract partial class CESharedActionSystem
{
    private void InitializeExamine()
    {
        SubscribeLocalEvent<ActionComponent, ExaminedEvent>(OnActionExamined);
        SubscribeLocalEvent<CEActionManaCostComponent, ExaminedEvent>(OnManacostExamined);
        SubscribeLocalEvent<CEActionStaminaCostComponent, ExaminedEvent>(OnStaminaCostExamined);

        SubscribeLocalEvent<CEActionFreeHandsRequiredComponent, ExaminedEvent>(OnSomaticExamined);
        SubscribeLocalEvent<CEActionTargetMobStatusRequiredComponent, ExaminedEvent>(OnMobStateExamined);
    }

    private void OnActionExamined(Entity<ActionComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.UseDelay is null)
            return;
        args.PushMarkup($"{Loc.GetString("ce-magic-cooldown")}: [color=#5da9e8]{ent.Comp.UseDelay.Value.TotalSeconds}s[/color]", priority: 9);
    }

    private void OnManacostExamined(Entity<CEActionManaCostComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup($"{Loc.GetString("ce-magic-manacost")}: [color=#5da9e8]{ent.Comp.ManaCost}[/color]", priority: 9);
    }

    private void OnStaminaCostExamined(Entity<CEActionStaminaCostComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup($"{Loc.GetString("ce-magic-staminacost")}: [color=#3fba54]{ent.Comp.Stamina}[/color]", priority: 9);
    }

    private void OnSomaticExamined(Entity<CEActionFreeHandsRequiredComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("ce-magic-somatic-aspect") + " " + ent.Comp.FreeHandRequired, 8);
    }

    private void OnMobStateExamined(Entity<CEActionTargetMobStatusRequiredComponent> ent, ref ExaminedEvent args)
    {
        var states = "";
        foreach (var state in ent.Comp.AllowedStates)
        {
            if (states.Length > 0)
                states += ", ";

            switch (state)
            {
                case CEMobState.Alive:
                    states += Loc.GetString("ce-magic-spell-target-mob-state-live");
                    break;
                case CEMobState.Critical:
                    states += Loc.GetString("ce-magic-spell-target-mob-state-critical");
                    break;
            }
        }

        args.PushMarkup(Loc.GetString("ce-magic-spell-target-mob-state", ("state", states)));
    }
}
