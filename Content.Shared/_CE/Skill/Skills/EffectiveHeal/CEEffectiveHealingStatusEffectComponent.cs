using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.EffectiveHeal;

/// <summary>
/// Increases all outgoing healing from the player by X
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectiveHealingStatusEffectComponent : Component
{
    [DataField]
    public int AdditionalHeal = 1;
}
