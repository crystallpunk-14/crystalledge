using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.Stamina;

/// <summary>
/// Tracks entity stamina. When stamina reaches 0, the entity enters an exhausted state
/// with a movement speed penalty until stamina fully recovers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CEStaminaSystem))]
public sealed partial class CEStaminaComponent : Component
{
    /// <summary>
    /// Maximum stamina value.
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
    /// Stamina regeneration per second after the cooldown expires.
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
    /// Movement speed multiplier applied while exhausted (0.7 = 30% slower).
    /// </summary>
    [DataField]
    public float ExhaustedSpeedModifier = 0.6f;
}
