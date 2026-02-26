using System.Linq;
using Content.Shared._CE.Camera;
using Content.Shared._CE.Weapon;
using Content.Shared.Effects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._CE.Weapon;

public sealed class CEClientMeleeWeaponSystem : CESharedMeleeWeaponSystem
{
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CEScreenshakeSystem _shake = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private readonly EntProtoId _attackImpact = "CEAttackImpact";
    private readonly EntProtoId _attackImpact2 = "CEAttackImpact2";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<CEMeleeAttackEffectEvent>(OnAttackEffectEvent);
    }

    protected override void RaiseAttackEffects(EntityUid user, List<EntityUid> targets)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        // This handles the prediction case for the attacking player
        OnAttackEffectEvent(new CEMeleeAttackEffectEvent(GetNetEntity(user), GetNetEntityList(targets)));
    }

    private void OnAttackEffectEvent(CEMeleeAttackEffectEvent args)
    {
        var user = GetEntity(args.User);
        var targets = GetEntityList(args.Targets);

        var userShakeRotation = new CEScreenshakeParameters() { Trauma = 0.12f, DecayRate = 1.25f, Frequency = 0.0015f };
        var otherShakeTranslation = new CEScreenshakeParameters() { Trauma = 0.35f, DecayRate = 2f, Frequency = 0.008f };

        // Spawn visual effects for each target
        foreach (var target in targets)
        {
            if (!Exists(target))
                continue;

            var direction = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(user);

            // Spawn impact effects
            var impact = Spawn(_attackImpact, Transform(target).Coordinates);
            _transform.SetWorldRotation(impact, direction.ToAngle());

            for (var i = 0; i < 3; i++)
            {
                var impact2 = Spawn(_attackImpact2, Transform(target).Coordinates);
                _transform.SetWorldRotation(impact2, direction.ToAngle() + _random.NextAngle(-1, 1));
            }

            // Apply screenshake to target
            _shake.Screenshake(target, otherShakeTranslation, null);
        }

        // Apply screenshake to attacker
        if (targets.Any())
            _shake.Screenshake(user, userShakeRotation, null);

        // Apply color flash effect
        if (_player.LocalEntity == user)
            _color.RaiseEffect(Color.Red, targets, Filter.Local());
        else
            _color.RaiseEffect(Color.Red, targets, Filter.Pvs(user, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == user));
    }
}
