using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Music;

/// <summary>
/// Defines the four-stage soundtrack for a boss encounter.
/// Stages are driven by <see cref="CEBossMusicState"/> and play in order:
/// Prelude (loop) -> Intro (one-shot) + Main (loop, delayed) -> Victory (one-shot).
/// </summary>
[Prototype("CEBossMusic")]
public sealed partial class CEBossMusicPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    /// <summary>Loops on the map before the fight starts.</summary>
    [DataField]
    public SoundSpecifier? Prelude;

    /// <summary>Short one-shot played at the moment the fight begins.</summary>
    [DataField]
    public SoundSpecifier? Intro;

    /// <summary>Looping main combat theme. Starts <see cref="MainStartDelay"/> seconds after <see cref="Intro"/>.</summary>
    [DataField]
    public SoundSpecifier? Main;

    /// <summary>One-shot played once when the boss is defeated. No music after it finishes.</summary>
    [DataField]
    public SoundSpecifier? Victory;

    /// <summary>Seconds after the intro starts before the main loop begins.</summary>
    [DataField]
    public float MainStartDelay = 5f;
}
