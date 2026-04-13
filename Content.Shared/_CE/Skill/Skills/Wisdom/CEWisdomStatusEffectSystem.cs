using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Skill.Skills.Wisdom;

public sealed partial class CEWisdomStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CESharedMagicEnergySystem _mana = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEWisdomStatusEffectComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEWisdomStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEWisdomStatusEffectComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEWisdomStatusEffectComponent, StatusEffectRelayedEvent<CECalculateMaxManaEvent>>(OnCalculateMaxMana);
    }

    private void OnApply(Entity<CEWisdomStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _mana.RefreshMaxMana(args.Target);
    }

    private void OnRemoved(Entity<CEWisdomStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _mana.RefreshMaxMana(args.Target);
    }

    private void OnStackEdited(Entity<CEWisdomStatusEffectComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _mana.RefreshMaxMana(args.Target);
    }

    private void OnCalculateMaxMana(Entity<CEWisdomStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateMaxManaEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatManaBonus * stacks;
    }
}
