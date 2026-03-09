using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffectStacks;

/// <summary>
/// Component for managing stacked status effects.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatusEffectStackSystem))]
[EntityCategory("StatusEffects")]
public sealed partial class CEStatusEffectStackComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stack = 1;

    /// <summary>
    /// Base duration of the status effect per stack.
    /// Used when resetting the timer upon expiration.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? BaseDuration = null;

    /// <summary>
    /// Used for Appearance system to modify visuals of status effect
    /// </summary>
    [DataField]
    public int MediumAppearance = 5;

    /// <summary>
    /// Used for Appearance system to modify visuals of status effect
    /// </summary>
    [DataField]
    public int HighAppearance = 10;
}
