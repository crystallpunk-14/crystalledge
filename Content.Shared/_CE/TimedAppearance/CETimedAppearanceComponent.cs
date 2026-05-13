using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.TimedAppearance;

/// <summary>
/// Tracks an in-progress timed appearance override on an entity.
/// Added by <see cref="CETimedAppearanceSystem.SetTimedAppearance"/> and removed automatically
/// when the timer expires or a new override is started.
/// This component is server-side only and is never networked —
/// visual propagation happens through <see cref="Robust.Shared.GameObjects.AppearanceComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CETimedAppearanceComponent : Component
{
    /// <summary>
    /// Server game time at which the override expires and appearance is restored.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    [AutoNetworkedField]
    public TimeSpan EndTime;
}

[Serializable, NetSerializable]
public enum CETimedAppearanceVisuals : byte
{
    /// <summary>
    /// The currently active named visual state. Empty string = no override (idle).
    /// </summary>
    ActiveKey,
}
