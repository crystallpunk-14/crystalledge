using Content.Shared._CE.DamageStatusEffect.Components;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.DamageStatusEffect;

public sealed partial class CESpeedModifierStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CESpeedModifierStatusEffectComponent, StatusEffectAppliedEvent>(OnApply);
        SubscribeLocalEvent<CESpeedModifierStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CESpeedModifierStatusEffectComponent, CEStatusEffectStackEditedEvent>(OnStackEdited);

        SubscribeLocalEvent<CESpeedModifierStatusEffectComponent, StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnCalculateSpeed);
    }

    private void OnApply(Entity<CESpeedModifierStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.Target);
    }

    private void OnRemoved(Entity<CESpeedModifierStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.Target);
    }

    private void OnStackEdited(Entity<CESpeedModifierStatusEffectComponent> ent, ref CEStatusEffectStackEditedEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.Target);
    }

    private void OnCalculateSpeed(Entity<CESpeedModifierStatusEffectComponent> ent, ref StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        var stack = 1;

        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            stack = stackComp.Stacks;

        for (var i = 0; i < stack; i++)
        {
            args.Args.ModifySpeed(ent.Comp.Speed);
        }
    }
}
