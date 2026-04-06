using Content.Shared._CE.Stamina;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Skill.Skills.Agility;

public sealed partial class CEAgilityStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CEStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEAgilityStatusEffectComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEAgilityStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEAgilityStatusEffectComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEAgilityStatusEffectComponent, StatusEffectRelayedEvent<CECalculateMaxStaminaEvent>>(OnCalculateMaxStamina);
    }

    private void OnApply(Entity<CEAgilityStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _stamina.RefreshMaxStamina(args.Target);
    }

    private void OnRemoved(Entity<CEAgilityStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _stamina.RefreshMaxStamina(args.Target);
    }

    private void OnStackEdited(Entity<CEAgilityStatusEffectComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _stamina.RefreshMaxStamina(args.Target);
    }

    private void OnCalculateMaxStamina(Entity<CEAgilityStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateMaxStaminaEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatStaminaBonus * stacks;
    }
}
