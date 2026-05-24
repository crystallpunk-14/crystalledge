using Content.Shared._CE.Health.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Skills.ChangeHealType;

/// <summary>
/// Replaces healing with damage of the specified <see cref="Target"/> damage type.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEChangeHealTypeStatusEffectComponent : Component
{
    [DataField]
    public ProtoId<CEDamageTypePrototype> Target;

    /// <summary>
    /// Multiplier applied to the original heal amount to calculate the damage dealt.
    /// </summary>
    [DataField]
    public float DamageMultiplier = 1f;

    [DataField]
    public EntProtoId Vfx = "CEEffectApostasyFire";

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_CE/Effects/apostasy_fire.ogg")
    {
        Params = AudioParams.Default.WithVariation(0.1f),
    };
}
