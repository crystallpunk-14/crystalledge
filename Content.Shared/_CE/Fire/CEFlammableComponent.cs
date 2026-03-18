using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Fire;

/// <summary>
/// Placed on entities that can burn. Controls how fire behaves on this entity:
/// cycle duration, whether fire grows or diminishes, and an optional sprite override.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEFlammableComponent : Component
{
    /// <summary>
    /// Duration of each burn cycle (time between stack ticks).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BurnCycleDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How many fire stacks change per cycle on this entity.
    /// Negative = fire diminishes, positive = fire grows, zero = stable.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int StackDelta = -1;
}
