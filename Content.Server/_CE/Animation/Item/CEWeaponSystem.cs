using Content.Shared._CE.Animation.Item;
using Robust.Shared.Player;

namespace Content.Server._CE.Animation.Item;

public sealed class CEWeaponSystem : CESharedWeaponSystem
{
    protected override void RaiseAttackEffects(EntityUid user, List<EntityUid> targets)
    {
        base.RaiseAttackEffects(user, targets);

        var filter = Filter.PvsExcept(user, entityManager: EntityManager);
        RaiseNetworkEvent(new CEMeleeAttackEffectEvent(GetNetEntity(user), GetNetEntityList(targets)), filter);
    }
}
