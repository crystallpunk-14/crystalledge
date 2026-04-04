using Content.Shared._CE.Camera;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Effects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CE.Health;

public sealed class CEDamageableSystem : CESharedDamageableSystem
{
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly CEScreenshakeSystem _shake = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageableComponent, AfterAutoHandleStateEvent>(OnDamageableAfterState);
    }

    private void OnDamageableAfterState(EntityUid uid, CEDamageableComponent comp, ref AfterAutoHandleStateEvent args)
    {
        var ev = new CEDamageChangedEvent(uid, comp.TotalDamage, comp.TotalDamage);
        RaiseLocalEvent(uid, ev, true);
    }

    protected override void RaiseDamageEffect(EntityUid target, EntityUid? source, bool isCritical)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Local());

        var shakeTranslation = new CEScreenshakeParameters() { Trauma = 0.4f, DecayRate = 3f, Frequency = 0.008f };
        _shake.Screenshake(target, shakeTranslation, null);
    }
}

public sealed class CEClientMobStateSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMobStateComponent, AfterAutoHandleStateEvent>(OnMobStateAfterState);
    }

    private void OnMobStateAfterState(EntityUid uid, CEMobStateComponent comp, ref AfterAutoHandleStateEvent args)
    {
        var stateEv = new CEMobStateChangedEvent(uid, comp.CurrentState, comp.CurrentState);
        RaiseLocalEvent(uid, stateEv, true);

        var maxHealthEv = new CEMaxHealthChangedEvent(uid);
        RaiseLocalEvent(uid, maxHealthEv, true);
    }
}

public sealed class CEMaxHealthChangedEvent(EntityUid target) : EntityEventArgs
{
    public readonly EntityUid Target = target;
}
