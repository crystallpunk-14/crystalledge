using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Skills.HealingWaters;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEHealingWatersStatusEffectComponent : Component
{
    [DataField]
    public EntProtoId StatusProto = "CEStatusEffectWet";

    [DataField]
    public int AdditionalHeal = 1;
}
