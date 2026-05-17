using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Timing;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._CE.Tools;

public sealed class CELanternSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CELanternComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CELanternComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CELanternComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<CELanternComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
    }

    private void OnMapInit(Entity<CELanternComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent.Owner, ToggleableVisuals.Enabled, ent.Comp.Activated);
    }

    private void OnUseInHand(Entity<CELanternComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Toggle(ent, args.User);
    }

    private void OnActivateInWorld(Entity<CELanternComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Toggle(ent, args.User);
    }

    private void OnGetVerbs(Entity<CELanternComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString(ent.Comp.Activated ? ent.Comp.VerbToggleOff : ent.Comp.VerbToggleOn),
            Act = () => Toggle(ent, user),
        });
    }

    private void Toggle(Entity<CELanternComponent> ent, EntityUid user)
    {
        if (!_useDelay.TryResetDelay(ent.Owner, checkDelayed: true))
            return;

        ent.Comp.Activated = !ent.Comp.Activated;
        Dirty(ent);

        var sound = ent.Comp.Activated ? ent.Comp.TurnOnSound : ent.Comp.TurnOffSound;
        _audio.PlayPredicted(sound, ent.Owner, user);

        _appearance.SetData(ent.Owner, ToggleableVisuals.Enabled, ent.Comp.Activated);

        var ev = new ItemToggledEvent(Predicted: true, Activated: ent.Comp.Activated, User: user);
        RaiseLocalEvent(ent.Owner, ref ev);
    }
}
