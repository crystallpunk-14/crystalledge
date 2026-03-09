using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Robust.Shared.GameStates;

namespace Content.Client._CE.Health;

public sealed class CEHealthSystem : CESharedHealthSystem
{
    private readonly Dictionary<EntityUid, (int Health, int MaxHealth)> _previousValues = new();
    private readonly Dictionary<EntityUid, CEMobState> _previousStates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEHealthComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<CEHealthComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnAfterAutoHandleState(EntityUid uid, CEHealthComponent comp, ref AfterAutoHandleStateEvent args)
    {
        var currentValues = (comp.Health, comp.MaxHealth);

        if (!_previousValues.TryGetValue(uid, out var previousValues))
        {
            _previousValues[uid] = currentValues;
        }
        else
        {
            if (previousValues.Health != currentValues.Health || previousValues.MaxHealth != currentValues.MaxHealth)
            {
                _previousValues[uid] = currentValues;

                var changeEvent = new CEHealthChangedEvent(uid,
                    previousValues.Health,
                    currentValues.Health,
                    currentValues.MaxHealth);
                RaiseLocalEvent(changeEvent);
            }
        }

        if (!_previousStates.TryGetValue(uid, out var previousState))
        {
            _previousStates[uid] = comp.CurrentState;
        }
        else
        {
            if (previousState != comp.CurrentState)
            {
                _previousStates[uid] = comp.CurrentState;

                var changeEvent = new CEMobStateChangedEvent(uid, previousState, comp.CurrentState);
                RaiseLocalEvent(changeEvent);
            }
        }

    }

    private void OnComponentShutdown(EntityUid uid, CEHealthComponent comp, ComponentShutdown args)
    {
        _previousValues.Remove(uid);
        _previousStates.Remove(uid);
    }
}
