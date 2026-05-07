using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects;

/// <summary>
/// When the owner applies a specific tile effect (<see cref="SourceTileEffect"/>),
/// also applies an additional tile effect (<see cref="AdditionalTileEffect"/>) to the same tile.
/// Attach to a status effect entity to create passive skill synergies
/// (e.g. Cursed Flame: every fire → also curses; Sharp Floor: every freeze → also spawns ice spikes).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CETileEffectLinkComponent : Component
{
    [DataField(required: true)]
    public EntProtoId SourceTileEffect;

    [DataField(required: true)]
    public EntProtoId AdditionalTileEffect;

    [DataField]
    public int AdditionalTileEffectAmount = 1;
}
