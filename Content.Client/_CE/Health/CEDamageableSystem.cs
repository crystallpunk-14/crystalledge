using Content.Shared._CE.Camera;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Effects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
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

        SubscribeLocalEvent<CEDamageableComponent, ComponentHandleState>(OnHandleState);
    }

    /// <summary>
    /// Applies the authoritative server state and raises <see cref="CEDamageChangedEvent"/>
    /// for game-logic and visual systems when damage actually changed.
    /// This fires once per state diff — server-only damage (ranged, environmental) arrives here.
    /// </summary>
    private void OnHandleState(EntityUid uid, CEDamageableComponent comp, ref ComponentHandleState args)
    {
        if (args.Current is not CEDamageableComponentState state)
            return;

        var oldDamage = new CEDamageSpecifier(comp.Damage);

        comp.Damage = new CEDamageSpecifier(state.Damage);

        if (oldDamage.Equals(comp.Damage))
            return;

        var ev = new CEDamageChangedEvent(uid, oldDamage, comp.Damage, predicted: false);
        RaiseLocalEvent(uid, ev, true);
    }

    protected override void RaiseDamageEffect(EntityUid target, EntityUid? source)
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
    }
}
