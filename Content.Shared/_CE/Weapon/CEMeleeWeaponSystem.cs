using System.Linq;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._CE.Weapon;

public abstract class CESharedMeleeWeaponSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    public bool TryAttack(EntityUid user, Entity<CEMeleeWeaponComponent> weapon, List<EntityUid> targets)
    {
        List<EntityUid> hitted = new();
        foreach (var target in targets)
        {
            if (!HasComp<DamageableComponent>(target))
                continue;

            if (!_damageable.TryChangeDamage(target, weapon.Comp.Damage))
                continue;

            hitted.Add(target);
        }

        if (hitted.Any())
        {
            // Raise visual effects (overridden in client/server implementations)
            RaiseAttackEffects(user, hitted);
        }

        return true;
    }

    /// <summary>
    /// Override this method in client/server implementations to handle visual effects.
    /// </summary>
    protected abstract void RaiseAttackEffects(EntityUid user, List<EntityUid> targets);
}
