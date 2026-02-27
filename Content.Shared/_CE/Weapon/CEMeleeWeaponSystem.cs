using System.Linq;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Shared._CE.Weapon;

public abstract class CESharedMeleeWeaponSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public bool TryAttack(EntityUid user, Entity<CEMeleeWeaponComponent> weapon, List<EntityUid> targets, float power, string damageGroup = "default")
    {
        if (!weapon.Comp.DamageGroups.TryGetValue(damageGroup, out var damage))
        {
            Log.Error($"Trying to attack with damageGroup {damageGroup} on {ToPrettyString(weapon)}, but it doesn't exist on this weapon");
            return false;
        }

        List<EntityUid> hitted = new();
        foreach (var target in targets)
        {
            if (!HasComp<DamageableComponent>(target))
                continue;

            if (!_damageable.TryChangeDamage(target, damage * power))
                continue;

            hitted.Add(target);
        }

        if (hitted.Any())
        {
            RaiseAttackEffects(user, hitted);
        }

        return true;
    }

    /// <summary>
    /// Override this method in client/server implementations to handle visual effects.
    /// </summary>
    protected virtual void RaiseAttackEffects(EntityUid user, List<EntityUid> targets)
    {
        // Base implementation does nothing - effects are handled in client/server implementations
    }
}
