using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects.ContactEffect;

/// <summary>
/// Attach to a tile effect entity to apply status effects to entities that touch or are ticked by this tile effect.
/// </summary>
[RegisterComponent]
public sealed partial class CETileEffectContactEffectsComponent : Component
{
    /// <summary>
    /// Status effects to apply to affected entities on contact.
    /// Multiplied by tile effect stacks
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> ContactEffects = new();
}

