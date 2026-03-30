using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.ManaCore;

/// <summary>
/// Increases all ingoing mana restoring in X times
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEManaCoreStatusEffectComponent : Component
{
    [DataField]
    public float ManaRestoreMultiplier = 2;
}
