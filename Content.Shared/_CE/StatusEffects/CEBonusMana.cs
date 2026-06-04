using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEBonusManaComponent : Component
{
    [DataField]
    public int FlatManaBonus = 10;
}

public sealed partial class CEBonusManaSystem : EntitySystem
{
    [Dependency] private readonly CESharedMagicEnergySystem _mana = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBonusManaComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEBonusManaComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEBonusManaComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEBonusManaComponent, StatusEffectRelayedEvent<CECalculateMaxManaEvent>>(OnCalculateMaxMana);
    }

    private void OnApply(Entity<CEBonusManaComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _mana.RefreshMaxMana(args.Target);
    }

    private void OnRemoved(Entity<CEBonusManaComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _mana.RefreshMaxMana(args.Target);
    }

    private void OnStackEdited(Entity<CEBonusManaComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _mana.RefreshMaxMana(args.Target);
    }

    private void OnCalculateMaxMana(Entity<CEBonusManaComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateMaxManaEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatManaBonus * stacks;
    }
}
