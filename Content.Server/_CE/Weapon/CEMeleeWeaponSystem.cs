using Content.Shared._CE.Weapon;
using Robust.Shared.Player;

namespace Content.Server._CE.Weapon;

public sealed class CEMeleeWeaponSystem : CESharedMeleeWeaponSystem
{
    protected override void RaiseAttackEffects(EntityUid user, List<EntityUid> targets)
    {
        base.RaiseAttackEffects(user, targets);

        var filter = Filter.PvsExcept(user, entityManager: EntityManager);
        RaiseNetworkEvent(new CEMeleeAttackEffectEvent(GetNetEntity(user), GetNetEntityList(targets)), filter);
    }
}
