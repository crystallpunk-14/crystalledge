using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.Agility;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEAgilityStatusEffectComponent : Component
{
    [DataField]
    public float FlatStaminaBonus = 10f;
}
