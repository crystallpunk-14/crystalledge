using System.Linq;
using Content.Shared._CE.Camera;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Weapon;

public sealed partial class CEMeleeWeaponSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly CEScreenshakeSystem _shake = default!;

    public bool TryAttack(EntityUid user, Entity<CEMeleeWeaponComponent> weapon, List<EntityUid> targets)
    {
        var userShakeRotation = new CEScreenshakeParameters() { Trauma = 0.12f, DecayRate = 1.25f, Frequency = 0.0015f };
        var otherShakeTranslation = new CEScreenshakeParameters() { Trauma = 0.35f, DecayRate = 2f, Frequency = 0.008f };

        if (targets.Any())
            _shake.Screenshake(user, null, userShakeRotation);

        foreach (var target in targets)
        {
            _damageable.TryChangeDamage(target, weapon.Comp.Damage, false);
            _shake.Screenshake(target, otherShakeTranslation, null);
        }

        if (_player.LocalEntity == user)
        {
            if (_timing.IsFirstTimePredicted)
                _color.RaiseEffect(Color.Red, targets, Filter.Local());
        }
        else
        {
            _color.RaiseEffect(Color.Red, targets, Filter.Pvs(user, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == user));
        }
        return true;
    }
}
