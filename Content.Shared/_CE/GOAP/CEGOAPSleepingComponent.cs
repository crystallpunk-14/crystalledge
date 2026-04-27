using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.GOAP;

/// <summary>
/// Marker component for sleeping GOAP NPCs. While present, prevents the entity from
/// being woken via <see cref="CECheckGOAPAwakeEvent"/>. Must be removed explicitly
/// by <see cref="CEGOAPSleepingSystem"/> when a wake trigger fires (damage, proximity, etc.).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CEGOAPSleepingComponent : Component
{
    /// <summary>
    /// Radius within which a player's presence will wake this entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WakeRadius = 5f;

    /// <summary>
    /// How long after the component is added before the mob can be woken.
    /// Prevents immediate wake-up on spawn.
    /// </summary>
    [DataField]
    public TimeSpan WakeDelay = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Absolute game time before which wake attempts are ignored.
    /// Set automatically by <see cref="CEGOAPSleepingSystem"/> on component startup.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan WakeAt = TimeSpan.Zero;
}
