using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.Vitality;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEVitalityStatusEffectComponent : Component
{
    [DataField]
    public int FlatHealthBonus = 10;
}
