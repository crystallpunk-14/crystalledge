using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.HealingWaters;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEHealingWatersStatusEffectComponent : Component
{
    [DataField]
    public int AdditionalHeal = 1;
}
