using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.MeleeWeapon;
using Robust.Shared.Player;

namespace Content.Server._CE.MeleeWeapon;

public sealed class CEWeaponSystem : CESharedWeaponSystem
{
    private const int MaxTargets = 10;

    /// <summary>
    /// Extra tolerance added to the weapon's effective range for server validation.
    /// Accounts for network latency and position prediction differences.
    /// </summary>
    private const float RangeTolerance = 0.2f;

    /// <summary>
    /// Fallback validation range when no WeaponArcAttack effect is found on the weapon.
    /// </summary>
    private const float FallbackRange = 1f;

    protected override void RaiseAttackEffects(EntityUid user, List<EntityUid> targets)
    {
        base.RaiseAttackEffects(user, targets);

        var filter = CEFilter.ZPvsExcept(user, EntityManager);
        RaiseNetworkEvent(new CEMeleeAttackEffectEvent(GetNetEntity(user), GetNetEntityList(targets)), filter);
    }

    /// <summary>
    /// For player attacks, skip damage from the animation keyframe.
    /// Damage comes from the predicted <see cref="CEWeaponArcHitEvent"/> handled in the shared system.
    /// For NPCs (no attached session), apply damage directly since there is no client.
    /// </summary>
    public override void HandleArcAttackHit(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets, string? effectSlot)
    {
        if (HasComp<ActorComponent>(user))
        {
            // Clear targets so the nested effects loop in Effect() does nothing.
            // Damage will be applied via OnArcHitEvent → ApplyArcEffects instead.
            targets.Clear();
            return;
        }

        TryAttack(user, weapon, targets);
        ApplyArcEffects(user, weapon, targets, effectSlot);
    }

    protected override List<EntityUid> ValidateArcTargets(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets)
    {
        if (targets.Count > MaxTargets)
            targets = targets.GetRange(0, MaxTargets);

        var range = GetMaxEffectiveRange(weapon) + RangeTolerance;
        var validated = new List<EntityUid>();

        foreach (var target in targets)
        {
            if (!Exists(target) || target == user)
                continue;

            if (!Interaction.InRangeUnobstructed(user, target, range))
                continue;

            validated.Add(target);
        }

        return validated;
    }

    /// <summary>
    /// Computes the maximum effective range (range * 2) from all WeaponArcAttack effects
    /// defined in the weapon's effect slots.
    /// </summary>
    private float GetMaxEffectiveRange(Entity<CEWeaponComponent> weapon)
    {
        var maxRange = 0f;

        foreach (var effects in weapon.Comp.EffectSlots.Values)
        {
            foreach (var effect in effects)
            {
                if (effect is WeaponArcAttack arc && arc.Range > maxRange)
                    maxRange = arc.Range;
            }
        }

        return maxRange > 0f ? maxRange * 2 : FallbackRange;
    }
}
