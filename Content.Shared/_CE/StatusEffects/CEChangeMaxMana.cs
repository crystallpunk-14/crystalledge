using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEChangeMaxManaComponent : Component
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

public sealed partial class CEChangeMaxManaSystem : EntitySystem
{
    [Dependency] private CESharedMagicEnergySystem _mana = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEChangeMaxManaComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEChangeMaxManaComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEChangeMaxManaComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEChangeMaxManaComponent, StatusEffectRelayedEvent<CECalculateMaxManaEvent>>(OnCalculateMaxMana);
    }

    private void OnApply(Entity<CEChangeMaxManaComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _mana.RefreshMaxMana(args.Target);
    }

    private void OnRemoved(Entity<CEChangeMaxManaComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _mana.RefreshMaxMana(args.Target);
    }

    private void OnStackEdited(Entity<CEChangeMaxManaComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _mana.RefreshMaxMana(args.Target);
    }

    private void OnCalculateMaxMana(Entity<CEChangeMaxManaComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateMaxManaEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatChange + ent.Comp.FlatChangePerStack * stacks;
        args.Args.Multiplier += ent.Comp.MultiplierChange + ent.Comp.MultiplierChangePerStack * stacks;
    }
}
