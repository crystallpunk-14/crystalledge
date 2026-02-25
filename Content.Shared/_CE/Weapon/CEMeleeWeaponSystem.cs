using System.Linq;
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

    public bool TryAttack(EntityUid user, Entity<CEMeleeWeaponComponent> weapon, List<EntityUid> targets)
    {
        foreach (var target in targets)
        {
            _damageable.TryChangeDamage(target, weapon.Comp.Damage, false);
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
