using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffectStacks;

/// <summary>
/// Tracks who applied a status effect. Placed on the status effect entity.
/// Automatically set by CEEntityEffects that apply status effects.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEStatusEffectSourceComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Source;
}
