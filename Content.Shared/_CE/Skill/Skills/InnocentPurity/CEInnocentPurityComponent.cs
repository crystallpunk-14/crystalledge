using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.InnocentPurity;

/// <summary>
/// Heals the bearer for each debuff stack removed when their debuffs are cleansed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEInnocentPurityComponent : Component
{
    /// <summary>
    /// Amount of health restored per removed debuff stack.
    /// </summary>
    [DataField]
    public int HealPerStack = 1;
}
