using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Stamina.StaminaRegenBuff;

public sealed partial class CEStaminaRegenBuffSystem : EntitySystem
{
    [Dependency] private readonly CEStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStaminaRegenBuffComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEStaminaRegenBuffComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEStaminaRegenBuffComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEStaminaRegenBuffComponent, StatusEffectRelayedEvent<CECalculateStaminaRegenEvent>>(OnCalculateStaminaRegen);
    }

    private void OnApply(Entity<CEStaminaRegenBuffComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _stamina.RefreshStaminaRegen(args.Target);
    }

    private void OnRemoved(Entity<CEStaminaRegenBuffComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _stamina.RefreshStaminaRegen(args.Target);
    }

    private void OnStackEdited(Entity<CEStaminaRegenBuffComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _stamina.RefreshStaminaRegen(args.Target);
    }

    private void OnCalculateStaminaRegen(Entity<CEStaminaRegenBuffComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateStaminaRegenEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatRegenBonus * stacks;
    }
}
