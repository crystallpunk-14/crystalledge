using Content.Shared._CE.Stamina;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEChangeMaxStaminaComponent : Component
{
    /// <summary>
    /// Changes max health by flat amount (can be negative)
    /// </summary>
    [DataField]
    public int FlatChange = 10;

    /// <summary>
    /// Changes max health by flat amount per stack (can be negative)
    /// </summary>
    [DataField]
    public int FlatChangePerStack = 0;

    /// <summary>
    /// Added to global multiplier (can be negative)
    /// </summary>
    [DataField]
    public float MultiplierChange = 0;

    /// <summary>
    /// Added to global multiplier per stack (can be negative)
    /// </summary>
    [DataField]
    public float MultiplierChangePerStack = 0;
}

public sealed partial class CEChangeMaxStaminaSystem : EntitySystem
{
    [Dependency] private CEStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEChangeMaxStaminaComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEChangeMaxStaminaComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEChangeMaxStaminaComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEChangeMaxStaminaComponent, StatusEffectRelayedEvent<CECalculateMaxStaminaEvent>>(OnCalculateMaxStamina);
    }

    private void OnApply(Entity<CEChangeMaxStaminaComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _stamina.RefreshMaxStamina(args.Target);
    }

    private void OnRemoved(Entity<CEChangeMaxStaminaComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _stamina.RefreshMaxStamina(args.Target);
    }

    private void OnStackEdited(Entity<CEChangeMaxStaminaComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _stamina.RefreshMaxStamina(args.Target);
    }

    private void OnCalculateMaxStamina(Entity<CEChangeMaxStaminaComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateMaxStaminaEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatChange + ent.Comp.FlatChangePerStack * stacks;
        args.Args.Multiplier += ent.Comp.MultiplierChange + ent.Comp.MultiplierChangePerStack * stacks;
    }
}
