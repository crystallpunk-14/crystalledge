using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEChangeMaxHealthComponent : Component
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

public sealed partial class CEChangeMaxHealthSystem : EntitySystem
{
    [Dependency] private CEMobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEChangeMaxHealthComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEChangeMaxHealthComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEChangeMaxHealthComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEChangeMaxHealthComponent, StatusEffectRelayedEvent<CECalculateMaxHealthEvent>>(OnCalculateMaxHealth);
    }

    private void OnApply(Entity<CEChangeMaxHealthComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _mobState.RefreshMaxHealth(args.Target);
    }

    private void OnRemoved(Entity<CEChangeMaxHealthComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _mobState.RefreshMaxHealth(args.Target);
    }

    private void OnStackEdited(Entity<CEChangeMaxHealthComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _mobState.RefreshMaxHealth(args.Target);
    }

    private void OnCalculateMaxHealth(Entity<CEChangeMaxHealthComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateMaxHealthEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatChange + ent.Comp.FlatChangePerStack * stacks;
        args.Args.Multiplier += ent.Comp.MultiplierChange + ent.Comp.MultiplierChangePerStack * stacks;
    }
}
