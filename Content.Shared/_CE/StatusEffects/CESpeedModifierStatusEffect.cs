using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CESpeedModifierStatusEffectComponent : Component
{
    /// <summary>
    ///
    /// </summary>
    [DataField]
    public float Speed = 1;
}

public sealed partial class CESpeedModifierStatusEffectSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

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
