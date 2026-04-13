using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Frost;

/// <summary>
/// Placed on entities that can be slowed by frost.
/// Subscribes to <see cref="CEFreezedEvent"/> to apply cold slowdown stacks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEFreezableComponent : Component
{
    /// <summary>
    /// Status effect prototype applied when this entity is frozen.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId StatusEffect = "CEStatusEffectColdSlowdown";

    /// <summary>
    /// Default duration of each cold cycle if not overridden by the caller.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);
}
