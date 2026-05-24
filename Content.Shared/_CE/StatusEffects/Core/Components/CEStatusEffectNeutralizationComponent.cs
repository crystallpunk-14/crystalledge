using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffectStacks;

/// <summary>
/// When placed on a status effect entity, neutralizes incoming stacks of specified effects.
/// Removes own stacks 1:1 against incoming stacks and reduces or cancels the incoming effect.
/// </summary>
[RegisterComponent]
public sealed partial class CEStatusEffectNeutralizationComponent : Component
{
    /// <summary>
    /// Set of status effect prototype IDs that this effect will neutralize.
    /// </summary>
    [DataField(required: true)]
    public HashSet<EntProtoId> Neutralizes = new();

    /// <summary>
    /// Optional VFX entity to spawn when neutralization occurs (e.g. steam).
    /// </summary>
    [DataField]
    public EntProtoId? Vfx;

    /// <summary>
    /// Optional sound to play when neutralization occurs.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;
}
