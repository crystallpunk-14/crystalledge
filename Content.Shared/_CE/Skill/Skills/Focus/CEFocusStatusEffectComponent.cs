using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.Focus;

/// <summary>
/// Status effect component that grants critical strikes.
/// Each stack represents one guaranteed critical hit.
/// When <see cref="CEIsCriticalDamageEvent"/> is relayed from the attacker,
/// one stack is consumed and the hit is marked as critical.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEFocusStatusEffectComponent : Component
{
}
