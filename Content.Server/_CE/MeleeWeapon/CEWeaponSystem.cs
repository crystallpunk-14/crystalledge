using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Animation.Item;

namespace Content.Server._CE.Animation.Item;

public sealed class CEWeaponSystem : CESharedWeaponSystem
{
    protected override void RaiseAttackEffects(EntityUid user, List<EntityUid> targets)
    {
        base.RaiseAttackEffects(user, targets);

        var filter = CEFilter.ZPvsExcept(user, EntityManager);
        RaiseNetworkEvent(new CEMeleeAttackEffectEvent(GetNetEntity(user), GetNetEntityList(targets)), filter);
    }
}
