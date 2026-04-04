using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.Focus;

/// <summary>
/// Status effect component that grants guaranteed critical strikes
/// for the entire duration of the effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEFocusStatusEffectComponent : Component
{
}
