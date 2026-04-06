using Robust.Shared.Audio;
using Robust.Shared.GameObjects;

namespace Content.Shared._CE.Audio.Components;

/// <summary>
/// Plays a sound when two stacks are merged together.
/// </summary>
[RegisterComponent]
public sealed partial class CEEmitSoundOnStackMergeComponent : Component
{
    /// <summary>
    /// Sound played when stacks are merged.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? Sound;
}
