using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.DivineShield;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEDivineShieldStatusEffectComponent : Component
{
    [DataField]
    public EntProtoId? BreakVfx = "CEEffectBreakDivineShield";

    [DataField]
    public SoundSpecifier? BreakSound;
}
