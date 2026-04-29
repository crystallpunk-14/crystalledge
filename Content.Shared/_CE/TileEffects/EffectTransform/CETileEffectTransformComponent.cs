using Content.Shared._CE.TileEffects.Core;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects.EffectTransform;

/// <summary>
/// When an entity with this component is touched or ticked by a tile effect
/// (via <see cref="CEAffectedByTileEffectEvent"/>), the entity is deleted and replaced
/// with the mapped prototype, preserving rotation.
/// </summary>
[RegisterComponent]
public sealed partial class CETileEffectTransformComponent : Component
{
    /// <summary>
    /// Maps triggering tile effect entity prototype ID → entity prototype to spawn as replacement.
    /// When a matching tile effect touches or ticks this entity, it is deleted and the mapped prototype is spawned,
    /// preserving the entity's rotation.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, EntProtoId> Transforms = new();

    [DataField]
    public bool DeleteSourceTileEffect = true;
}

