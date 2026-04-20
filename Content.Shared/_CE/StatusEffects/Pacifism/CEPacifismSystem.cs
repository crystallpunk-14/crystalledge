using Content.Shared.Interaction.Events;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;

namespace Content.Shared._CE.StatusEffects.Pacifism;

/// <summary>
/// Blocks combat actions (attacks, throws, item usage) when the
/// <see cref="CEPacifismEffectComponent"/> status effect is active.
/// Uses <see cref="StatusEffectRelayedEvent{T}"/> — events are relayed from the player
/// to each active status-effect entity.
/// </summary>
public sealed class CEPacifismSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEPacifismEffectComponent, StatusEffectRelayedEvent<AttackAttemptEvent>>(OnAttackAttempt);
        SubscribeLocalEvent<CEPacifismEffectComponent, StatusEffectRelayedEvent<UseAttemptEvent>>(OnUseAttempt);
        SubscribeLocalEvent<CEPacifismEffectComponent, StatusEffectRelayedEvent<ThrowAttemptEvent>>(OnThrowAttempt);
    }

    private void OnAttackAttempt(Entity<CEPacifismEffectComponent> ent, ref StatusEffectRelayedEvent<AttackAttemptEvent> args)
    {
        args.Args.Cancel();
    }

    private void OnUseAttempt(Entity<CEPacifismEffectComponent> ent, ref StatusEffectRelayedEvent<UseAttemptEvent> args)
    {
        args.Args.Cancel();
    }

    private void OnThrowAttempt(Entity<CEPacifismEffectComponent> ent, ref StatusEffectRelayedEvent<ThrowAttemptEvent> args)
    {
        args.Args.Cancel();
    }
}
