using Content.Shared._CE.Health.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Standing;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Health;

/// <summary>
/// Manages CE mob states (Alive, Critical, Dead) based on <see cref="CEHealthComponent"/> values.
/// Critical state is entered when health reaches 0, Dead when health reaches DeathThreshold.
/// </summary>
public abstract partial class CESharedHealthSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private void InitMobState()
    {
        SubscribeLocalEvent<CEHealthComponent, CEHealthChangedEvent>(OnHealthChanged);
        SubscribeLocalEvent<CEHealthComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<CEHealthComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<CEHealthComponent>(ent, out var health))
            return;

        UpdateState(ent, health);
    }

    private void OnHealthChanged(Entity<CEHealthComponent> ent, ref CEHealthChangedEvent args)
    {
        if (!TryComp<CEHealthComponent>(ent, out var health))
            return;

        UpdateState(ent, health);
    }

    private void UpdateState(Entity<CEHealthComponent> ent, CEHealthComponent health)
    {
        var newState = CalculateState(health);

        if (newState == ent.Comp.CurrentState)
            return;

        var oldState = ent.Comp.CurrentState;
        ent.Comp.CurrentState = newState;
        Dirty(ent);

        _appearance.SetData(ent, CEHealthState.State, newState);

        if (!_timing.ApplyingState)
        {
            OnStateExited(ent, oldState);
            OnStateEntered(ent, newState);

            var ev = new CEMobStateChangedEvent(ent, oldState, newState);
            RaiseLocalEvent(ent, ev, true);
        }
    }

    private CEMobState CalculateState(CEHealthComponent health)
    {
        if (health.Health <= health.DeathThreshold)
            return CEMobState.Dead;

        if (health.Health <= 0)
            return CEMobState.Critical;

        return CEMobState.Alive;
    }

    private void OnStateEntered(EntityUid target, CEMobState state)
    {
        _blocker.UpdateCanMove(target);

        switch (state)
        {
            case CEMobState.Alive:
                _standing.Stand(target);
                break;
            case CEMobState.Critical:
                _standing.Down(target);
                var dropEv = new DropHandItemsEvent();
                RaiseLocalEvent(target, ref dropEv);
                break;
            case CEMobState.Dead:
                _standing.Down(target);
                var dropDeadEv = new DropHandItemsEvent();
                RaiseLocalEvent(target, ref dropDeadEv);
                break;
        }
    }

    private void OnStateExited(EntityUid target, CEMobState state)
    {
        switch (state)
        {
            case CEMobState.Critical:
            case CEMobState.Dead:
                _standing.Stand(target);
                break;
        }
    }

    public bool IsAlive(EntityUid uid, CEHealthComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.CurrentState == CEMobState.Alive;
    }

    public bool IsCritical(EntityUid uid, CEHealthComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.CurrentState == CEMobState.Critical;
    }

    public bool IsDead(EntityUid uid, CEHealthComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.CurrentState == CEMobState.Dead;
    }

    public bool IsIncapacitated(EntityUid uid, CEHealthComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.CurrentState is CEMobState.Critical or CEMobState.Dead;
    }
}

/// <summary>
/// Raised when a CE mob state changes.
/// </summary>
public sealed class CEMobStateChangedEvent(EntityUid target, CEMobState oldState, CEMobState newState)
    : EntityEventArgs
{
    public readonly EntityUid Target = target;
    public readonly CEMobState OldState = oldState;
    public readonly CEMobState NewState = newState;
}
