using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffectStacks;

/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatusEffectStackSystem))]
[EntityCategory("StatusEffects")]
public sealed partial class CEStatusEffectStackComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stack = 1;
}
