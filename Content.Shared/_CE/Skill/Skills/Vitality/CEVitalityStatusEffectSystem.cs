using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Skill.Skills.Vitality;

public sealed partial class CEVitalityStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEVitalityStatusEffectComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CEVitalityStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CEVitalityStatusEffectComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);
        SubscribeLocalEvent<CEVitalityStatusEffectComponent, StatusEffectRelayedEvent<CECalculateMaxHealthEvent>>(OnCalculateMaxHealth);
    }

    private void OnApply(Entity<CEVitalityStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _mobState.RefreshMaxHealth(args.Target);
    }

    private void OnRemoved(Entity<CEVitalityStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _mobState.RefreshMaxHealth(args.Target);
    }

    private void OnStackEdited(Entity<CEVitalityStatusEffectComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _mobState.RefreshMaxHealth(args.Target);
    }

    private void OnCalculateMaxHealth(Entity<CEVitalityStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CECalculateMaxHealthEvent> args)
    {
        var stacks = 1;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stacks = stackComp.Stacks;

        args.Args.FlatModifier += ent.Comp.FlatHealthBonus * stacks;
    }
}
