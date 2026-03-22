using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CE.Health;

public sealed class CEDamageableSystem : CESharedDamageableSystem
{
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, int> _previousDamage = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageableComponent, AfterAutoHandleStateEvent>(OnDamageableAfterState);
        SubscribeLocalEvent<CEDamageableComponent, ComponentShutdown>(OnDamageableShutdown);
    }

    private void OnDamageableAfterState(EntityUid uid, CEDamageableComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!_previousDamage.TryGetValue(uid, out var previousDamage))
        {
            _previousDamage[uid] = comp.TotalDamage;
        }
        else if (previousDamage != comp.TotalDamage)
        {
            _previousDamage[uid] = comp.TotalDamage;
            var ev = new CEDamageChangedEvent(uid, previousDamage, comp.TotalDamage);
            RaiseLocalEvent(uid, ev, true);
        }
    }

    private void OnDamageableShutdown(EntityUid uid, CEDamageableComponent comp, ComponentShutdown args)
    {
        _previousDamage.Remove(uid);
    }

    protected override void RaiseDamageEffect(EntityUid target, EntityUid? source)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Local());
    }
}

public sealed class CEClientMobStateSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, CEMobState> _previousStates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMobStateComponent, AfterAutoHandleStateEvent>(OnMobStateAfterState);
        SubscribeLocalEvent<CEMobStateComponent, ComponentShutdown>(OnMobStateShutdown);
    }

    private void OnMobStateAfterState(EntityUid uid, CEMobStateComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!_previousStates.TryGetValue(uid, out var previousState))
        {
            _previousStates[uid] = comp.CurrentState;
        }
        else if (previousState != comp.CurrentState)
        {
            _previousStates[uid] = comp.CurrentState;
            var ev = new CEMobStateChangedEvent(uid, previousState, comp.CurrentState);
            RaiseLocalEvent(uid, ev, true);
        }
    }

    private void OnMobStateShutdown(EntityUid uid, CEMobStateComponent comp, ComponentShutdown args)
    {
        _previousStates.Remove(uid);
    }
}
