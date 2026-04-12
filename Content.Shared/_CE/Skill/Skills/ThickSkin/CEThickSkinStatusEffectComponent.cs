using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.ThickSkin;

/// <summary>
/// Multiplies all incoming temporary shield stacks.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEThickSkinStatusEffectComponent : Component
{
    [DataField]
    public float TempShieldMultiplier = 2;
}
