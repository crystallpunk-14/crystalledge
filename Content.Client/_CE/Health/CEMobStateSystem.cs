using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Robust.Shared.GameStates;

namespace Content.Client._CE.Health;

public sealed class CEMobStateSystem : CESharedMobStateSystem
{
    private readonly Dictionary<EntityUid, CEMobState> _previousStates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMobStateComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<CEMobStateComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnAfterAutoHandleState(EntityUid uid, CEMobStateComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!_previousStates.TryGetValue(uid, out var previousState))
        {
            _previousStates[uid] = comp.CurrentState;
            return;
        }

        if (previousState != comp.CurrentState)
        {
            _previousStates[uid] = comp.CurrentState;

            var changeEvent = new CEMobStateChangedEvent(uid, previousState, comp.CurrentState);
            RaiseLocalEvent(changeEvent);
        }
    }

    private void OnComponentShutdown(EntityUid uid, CEMobStateComponent comp, ComponentShutdown args)
    {
        _previousStates.Remove(uid);
    }
}
