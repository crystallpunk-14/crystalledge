using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.DivineShield;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEDivineShieldStatusEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Applier;

    [DataField]
    public EntProtoId? BreakVfx = "CEEffectBreakDivineShield";

    [DataField]
    public SoundSpecifier? BreakSound = new SoundPathSpecifier("/Audio/_CE/Effects/divine_shield_break.ogg")
    {
        Params = AudioParams.Default.WithVariation(0.1f)
    };
}
