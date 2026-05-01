using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Skills.CursedFlame;

/// <summary>
/// When the owner applies a fire tile effect, also applies 1 stack of curse to the same tile.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CECursedFlameStatusEffectComponent : Component
{
    [DataField]
    public EntProtoId SourceTileEffect = "CETileEffectFire";

    [DataField]
    public EntProtoId AdditionalTileEffect = "CETileEffectCurse";

    [DataField]
    public int AdditionalTileEffectAmount = 1;
}
