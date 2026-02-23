using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.Masochism;

/// <summary>
/// Restore mana when taking damage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEMasochismStatusEffectComponent : Component
{
    [DataField]
    public int ManaRestore = 1;
}
