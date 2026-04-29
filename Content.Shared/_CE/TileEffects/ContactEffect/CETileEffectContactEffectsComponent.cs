using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects.ContactEffect;

/// <summary>
/// Attach to a tile effect entity to apply status effects to entities that touch or are ticked by this tile effect.
/// Extracted from <see cref="CETileEffectComponent.ContactEffects"/> into a dedicated optional component.
/// </summary>
[RegisterComponent]
public sealed partial class CETileEffectContactEffectsComponent : Component
{
    /// <summary>
    /// Status effects to apply to affected entities on contact.
    /// Values are base stack amounts; the actual stacks applied = value × current tile <see cref="CETileEffectComponent.Stacks"/>.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> ContactEffects = new();
}

