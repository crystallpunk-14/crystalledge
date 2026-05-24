using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.ToggleableStatusEffectAction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEToggleableActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;
}
