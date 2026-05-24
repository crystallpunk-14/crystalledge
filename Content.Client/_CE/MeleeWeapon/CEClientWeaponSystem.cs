using System.Linq;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.Camera;
using Content.Shared._CE.MeleeWeapon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._CE.MeleeWeapon;

public sealed partial class CEClientWeaponSystem : CESharedWeaponSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CEScreenshakeSystem _shake = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    private readonly EntProtoId _attackImpact = "CEAttackImpact";
    private readonly EntProtoId _attackImpact2 = "CEAttackImpact2";
    private readonly EntProtoId _attackImpact3 = "CEAttackImpact3";

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        UpdatesOutsidePrediction = true;

        SubscribeAllEvent<CEMeleeAttackEffectEvent>(OnAttackEffectEvent);
    }

    private void OnAttackEffectEvent(CEMeleeAttackEffectEvent args)
    {
        var user = GetEntity(args.User);

        if (!Exists(user))
            return;

        var targets = GetEntityList(args.Targets);

        var otherShakeTranslation = new CEScreenshakeParameters() { Trauma = 0.4f, DecayRate = 3f, Frequency = 0.008f };
        var userShakeTranslation = new CEScreenshakeParameters() { Trauma = 0.5f, DecayRate = 3f, Frequency = 0.008f };

        // Apply screenshake to attacker if they're a local player
        if (_player.LocalSession?.AttachedEntity == user && targets.Any())
        {
            _shake.Screenshake(user, userShakeTranslation, null);
        }

        // Spawn visual effects for each target
        foreach (var target in targets)
        {
            if (!Exists(target))
                continue;

            var direction = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(user);

            // Spawn impact effects
            var impact = Spawn(_attackImpact, Transform(target).Coordinates);
            _transform.SetWorldRotation(impact, direction.ToAngle());

            for (var i = 0; i < 2; i++)
            {
                var impact2 = Spawn(_attackImpact2, Transform(target).Coordinates);
                _transform.SetWorldRotation(impact2, direction.ToAngle() + _random.NextAngle(-1, 1));
            }

            var impact3 = Spawn(_attackImpact3, Transform(target).Coordinates);
            _transform.SetWorldRotation(impact3, direction.ToAngle());

            // Apply screenshake to target
            _shake.Screenshake(target, otherShakeTranslation, null);
        }
    }

    protected override void RaiseAttackEffects(EntityUid user, List<EntityUid> targets)
    {
        base.RaiseAttackEffects(user, targets);

        if (!_timing.IsFirstTimePredicted)
            return;

        // This handles the prediction case for the attacking player
        OnAttackEffectEvent(new CEMeleeAttackEffectEvent(GetNetEntity(user), GetNetEntityList(targets)));
    }

    public override void HandleArcAttackHit(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets, string? effectSlot)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        // Send the client-calculated hit list as a predicted event.
        // The shared handler will call TryAttack both during prediction and on server.
        RaisePredictiveEvent(new CEWeaponArcHitEvent(
            GetNetEntity(weapon.Owner),
            GetNetEntityList(targets),
            effectSlot));
    }
}
