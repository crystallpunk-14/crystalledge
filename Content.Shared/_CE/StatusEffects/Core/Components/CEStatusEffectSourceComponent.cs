using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffectStacks;

/// <summary>
/// Tracks who applied a status effect. Placed on the status effect entity.
/// Automatically set by CEEntityEffects that apply status effects.
/// Uses manual state handling to avoid networking errors when the source entity is deleted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEStatusEffectSourceComponent : Component
{
    /// <summary>
    /// ALWAYS protect access to this field with Exists() checks. The source entity may have been deleted, and if so this will be null.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Source;
}
