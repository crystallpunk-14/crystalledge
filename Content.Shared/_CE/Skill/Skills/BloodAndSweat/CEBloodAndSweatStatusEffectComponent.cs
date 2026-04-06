using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.BloodAndSweat;

/// <summary>
/// Restores stamina when the entity takes damage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEBloodAndSweatStatusEffectComponent : Component
{
    [DataField]
    public float StaminaRestore = 5f;
}
