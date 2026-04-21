using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.BonusIncomingMana;

/// <summary>
/// Increases all ingoing mana restoring by X
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEBonusIncomingManaStatusEffectComponent : Component
{
    [DataField]
    public int Amount = 1;
}
