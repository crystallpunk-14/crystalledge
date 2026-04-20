using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.Fire;

/// <summary>
/// For tile fire entity
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEFireComponent : Component
{
    /// <summary>
    /// Current number of fire stacks on this tile. Can be infinite.
    /// At 0, the fire entity is deleted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Stacks = 1;

    /// <summary>
    /// Minimum seconds between decay ticks (loses 1 stack per tick).
    /// </summary>
    [DataField]
    public float MinDecayInterval = 2f;

    /// <summary>
    /// Maximum seconds between decay ticks (loses 1 stack per tick).
    /// </summary>
    [DataField]
    public float MaxDecayInterval = 5f;

    /// <summary>
    /// Next time a decay tick should happen.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextDecayTime = TimeSpan.Zero;

    /// <summary>
    /// Stack threshold for medium visual appearance.
    /// </summary>
    [DataField]
    public int MediumThreshold = 5;

    /// <summary>
    /// Stack threshold for high visual appearance.
    /// </summary>
    [DataField]
    public int HighThreshold = 10;
}
