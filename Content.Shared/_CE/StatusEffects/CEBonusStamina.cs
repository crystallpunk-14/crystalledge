using Content.Shared._CE.Stamina;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEBonusStaminaComponent : Component
{
    [DataField]
    public float FlatStaminaBonus = 10f;
}

public sealed partial class CEBonusStaminaSystem : EntitySystem
{
    [Dependency] private CEStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBonusStaminaComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEBonusStaminaComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEBonusStaminaComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEBonusStaminaComponent, StatusEffectRelayedEvent<CECalculateMaxStaminaEvent>>(OnCalculateMaxStamina);
    }

    private void OnApply(Entity<CEBonusStaminaComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _stamina.RefreshMaxStamina(args.Target);
    }

    private void OnRemoved(Entity<CEBonusStaminaComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _stamina.RefreshMaxStamina(args.Target);
    }

    private void OnStackEdited(Entity<CEBonusStaminaComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _stamina.RefreshMaxStamina(args.Target);
    }

    private void OnCalculateMaxStamina(Entity<CEBonusStaminaComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateMaxStaminaEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatStaminaBonus * stacks;
    }
}
