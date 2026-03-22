using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Animation.Item;
using Content.Shared._CE.Animation.Item.Components;
using Robust.Shared.Player;

namespace Content.Server._CE.Animation.Item;

public sealed class CEWeaponSystem : CESharedWeaponSystem
{
    private const int MaxTargets = 10;

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
    public override void HandleArcAttackHit(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets, float power)
    {
        if (HasComp<ActorComponent>(user))
            return;

        TryAttack(user, weapon, targets, power);
    }

    protected override List<EntityUid> ValidateArcTargets(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets)
    {
        if (targets.Count > MaxTargets)
            targets = targets.GetRange(0, MaxTargets);

        var range = 1.5f * weapon.Comp.RangeMultiplier + 0.5f;
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
}
