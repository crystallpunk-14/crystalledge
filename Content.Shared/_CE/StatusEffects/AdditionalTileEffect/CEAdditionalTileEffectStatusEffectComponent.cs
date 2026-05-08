using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.AdditionalTileEffect;

/// <summary>
/// When the owner applies a specific tile effect (<see cref="SourceTileEffect"/>),
/// also applies an additional tile effect (<see cref="AdditionalTileEffect"/>) to the same tile.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEAdditionalTileEffectStatusEffectComponent : Component
{
    [DataField(required: true)]
    public EntProtoId SourceTileEffect;

    [DataField(required: true)]
    public EntProtoId AdditionalTileEffect;

    [DataField]
    public int AdditionalTileEffectAmount = 1;
}
