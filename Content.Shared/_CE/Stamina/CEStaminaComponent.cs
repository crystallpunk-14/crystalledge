using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.Stamina;

/// <summary>
/// Tracks entity stamina. When stamina reaches 0, the entity enters an exhausted state
/// with a movement speed penalty until stamina fully recovers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(CEStaminaSystem))]
public sealed partial class CEStaminaComponent : Component
{
    /// <summary>
    /// Base maximum stamina before modifiers.
    /// Used as the starting value for <see cref="CECalculateMaxStaminaEvent"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BaseMaxStamina = 10f;

    /// <summary>
    /// Effective maximum stamina after modifiers (flat + multipliers).
    /// Set by <see cref="CEStaminaSystem.RefreshMaxStamina"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxStamina = 10f;

    /// <summary>
    /// Snapshot of stamina at the time of last state change.
    /// Actual current stamina is computed as: Stamina + elapsed regen.
    /// Use <see cref="CEStaminaSystem.GetStamina"/> to get the real value.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Stamina = 10f;

    /// <summary>
    /// Time in seconds for stamina to fully regenerate from 0 to max.
    /// The effective regen rate is computed as MaxStamina / FullRegenDuration.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FullRegenDuration = 5f;

    /// <summary>
    /// Effective stamina regeneration per second after modifiers.
    /// Computed from MaxStamina / FullRegenDuration, then modified by <see cref="CECalculateStaminaRegenEvent"/>.
    /// Set by <see cref="CEStaminaSystem.RefreshStaminaRegen"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RegenRate = 2f;

    /// <summary>
    /// How long after the last stamina use before regeneration begins.
    /// </summary>
    [DataField]
    public TimeSpan RegenCooldown = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The time at which stamina regeneration can begin.
    /// Both client and server use this + RegenRate to compute current stamina
    /// without networking every frame.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan RegenStartTime = TimeSpan.Zero;

    /// <summary>
    /// Whether the entity is currently exhausted (stamina hit 0).
    /// While exhausted, stamina cannot be spent and a speed penalty applies.
    /// Clears only when stamina is fully restored.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Exhausted;

    /// <summary>
    /// Next time a "not enough stamina" popup is allowed. Prevents spam.
    /// Not networked — tracked locally on the predicting side.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextPopupTime;

    /// <summary>
    /// Movement speed multiplier applied while exhausted (0.7 = 30% slower).
    /// </summary>
    [DataField]
    public float ExhaustedSpeedModifier = 0.6f;
    // ── Fatigue animation parameters ──

    /// <summary>
    /// Stamina ratio (current/max) below which the fatigue animation starts.
    /// 1.0 = always playing when not full, 0.5 = starts at half stamina.
    /// </summary>
    [DataField]
    public float AnimationThreshold = 0.5f;

    /// <summary>
    /// Minimum breathing animation frequency (cycles per second) at the start of fatigue.
    /// </summary>
    [DataField]
    public float FrequencyMin = 0.25f;

    /// <summary>
    /// Additional frequency added as fatigue increases (0 → max fatigue).
    /// </summary>
    [DataField]
    public float FrequencyMod = 1.75f;

    /// <summary>
    /// Minimum jitter amplitude at the start of fatigue.
    /// </summary>
    [DataField]
    public float JitterAmplitudeMin;

    /// <summary>
    /// Additional jitter amplitude as fatigue increases.
    /// </summary>
    [DataField]
    public float JitterAmplitudeMod = 0.04f;

    /// <summary>
    /// Minimum jitter offset bounds (X, Y).
    /// </summary>
    [DataField]
    public Vector2 JitterMin = new(0.5f, 0.125f);

    /// <summary>
    /// Maximum jitter offset bounds (X, Y).
    /// </summary>
    [DataField]
    public Vector2 JitterMax = new(1.0f, 0.25f);

    /// <summary>
    /// Minimum breathing amplitude at the start of fatigue.
    /// </summary>
    [DataField]
    public float BreathingAmplitudeMin = 0.04f;

    /// <summary>
    /// Additional breathing amplitude as fatigue increases.
    /// </summary>
    [DataField]
    public float BreathingAmplitudeMod = 0.04f;

    /// <summary>
    /// Number of jitter keyframes per breathing cycle.
    /// </summary>
    [DataField]
    public int Jitters = 4;

}
