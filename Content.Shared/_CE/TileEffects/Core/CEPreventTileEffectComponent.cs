using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects.Core;

/// <summary>
/// Blocks specific tile effects from being applied to the tile this entity is anchored on.
/// Attach to anchored tile entities (e.g. water tile) to prevent listed tile effects from spawning.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEPreventTileEffectComponent : Component
{
    /// <summary>
    /// Tile effect entity prototypes that are blocked by this component.
    /// An empty list blocks all tile effects.
    /// </summary>
    [DataField]
    public List<EntProtoId> Blocks = new();
}
