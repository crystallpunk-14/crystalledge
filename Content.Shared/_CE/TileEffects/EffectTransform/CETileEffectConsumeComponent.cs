using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects.EffectTransform;

/// <summary>
/// When placed on a tile effect entity, absorbs incoming tile effects from the listed prototypes
/// instead of letting them spawn separately. The incoming stacks are added to this entity.
/// Example: cursed fire consumes regular fire — applying fire to a cursed fire tile just makes
/// the cursed fire stronger rather than spawning a separate fire entity.
/// </summary>
[RegisterComponent]
public sealed partial class CETileEffectConsumeComponent : Component
{
    /// <summary>
    /// Tile effect prototype IDs that this entity will absorb.
    /// When one of these is applied to the same tile, its stacks are redirected into this entity.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Consumes = new();
}
