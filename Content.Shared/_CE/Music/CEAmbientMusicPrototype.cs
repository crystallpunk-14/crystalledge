using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Music;

/// <summary>
/// Defines a two-layered ambient music track.
/// Both layers are played simultaneously and must have equal length.
/// The active intensity level controls which layers are audible.
/// </summary>
[Prototype("CEAmbientMusic")]
public sealed partial class CEAmbientMusicPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Always audible. Plays at all intensity levels (0–1).
    /// </summary>
    [DataField]
    public SoundSpecifier? CalmLayer;

    /// <summary>
    /// Audible at intensity 1. Layered on top of <see cref="CalmLayer"/>.
    /// </summary>
    [DataField]
    public SoundSpecifier? BattleLayer;
}
