using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Robust.Shared.GameStates;

namespace Content.Client._CE.Health;

public sealed class CEHealthSystem : CESharedHealthSystem
{
    private readonly Dictionary<EntityUid, (int Health, int MaxHealth)> _previousValues = new();

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
            return;
        }

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

    private void OnComponentShutdown(EntityUid uid, CEHealthComponent comp, ComponentShutdown args)
    {
        _previousValues.Remove(uid);
    }
}
