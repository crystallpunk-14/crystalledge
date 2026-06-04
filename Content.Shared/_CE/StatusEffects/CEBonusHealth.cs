using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEBonusHealthComponent : Component
{
    [DataField]
    public int FlatHealthBonus = 10;
}

public sealed partial class CEBonusHealthSystem : EntitySystem
{
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBonusHealthComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEBonusHealthComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEBonusHealthComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEBonusHealthComponent, StatusEffectRelayedEvent<CECalculateMaxHealthEvent>>(OnCalculateMaxHealth);
    }

    private void OnApply(Entity<CEBonusHealthComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _mobState.RefreshMaxHealth(args.Target);
    }

    private void OnRemoved(Entity<CEBonusHealthComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _mobState.RefreshMaxHealth(args.Target);
    }

    private void OnStackEdited(Entity<CEBonusHealthComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _mobState.RefreshMaxHealth(args.Target);
    }

    private void OnCalculateMaxHealth(Entity<CEBonusHealthComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateMaxHealthEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatHealthBonus * stacks;
    }
}
