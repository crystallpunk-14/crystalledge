using Content.Shared._CE.Skills.Prototypes;
using Content.Shared._CE.SkillsUpgrade;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.SkillsUpgradeable;

public sealed partial class CEClientSkillUpgradeableSystem : CESharedSkillUpgradeableSystem
{
    public void RequestLearnSkill(Entity<CESkillUpgradeableComponent> target, ProtoId<CESkillPrototype> skill)
    {
        if (!target.Comp.CurrentUpgradeSelection.Contains(skill))
            return;

        var netEv = new CETryLearnSkillMessage(GetNetEntity(target), skill);
        RaiseNetworkEvent(netEv);
    }
}
