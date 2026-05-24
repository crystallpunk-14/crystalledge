using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.Wisdom;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEWisdomStatusEffectComponent : Component
{
    [DataField]
    public int FlatManaBonus = 10;
}
