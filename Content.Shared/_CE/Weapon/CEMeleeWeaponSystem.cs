using System.Linq;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._CE.Weapon;

public abstract class CESharedMeleeWeaponSystem : EntitySystem
{
    [Dependency] private readonly CESharedHealthSystem _health = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

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
            if (!_health.TakeDamage(target, damage * power, user))
                continue;

            var attackedEv = new CEAttackedEvent(user, weapon);
            RaiseLocalEvent(target, attackedEv);

            hitted.Add(target);
        }

        if (!hitted.Any())
            return false;

        //Attack confirmed

        RaiseAttackEffects(user, hitted);
        _audio.PlayPredicted(weapon.Comp.HitSound, weapon, user);

        var usedEv = new CEAttackUsingEvent(user, hitted);
        RaiseLocalEvent(weapon, usedEv);

        var attackerEv = new CEAfterAttackEvent(weapon, hitted);
        RaiseLocalEvent(user, attackerEv);

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


/// <summary>
/// Raised on used weapon when attack hits something.
/// </summary>
public sealed partial class CEAttackUsingEvent(EntityUid user, List<EntityUid> targets) : EntityEventArgs
{
    public EntityUid User = user;
    public List<EntityUid> Targets = targets;
}

/// <summary>
/// Raised on attacked entity when it gets hit by a CEMeleeWeaponComponent attack.
/// </summary>
public sealed partial class CEAttackedEvent(EntityUid attacker, EntityUid weapon)
{
    public EntityUid Attacker = attacker;
    public EntityUid Weapon = weapon;
}

/// <summary>
/// Raised on attacker, after it attacks something with a CEMeleeWeaponComponent
/// </summary>
public sealed partial class CEAfterAttackEvent(EntityUid weapon, List<EntityUid> targets)
{
    public EntityUid Weapon = weapon;
    public List<EntityUid> Targets = targets;
}
