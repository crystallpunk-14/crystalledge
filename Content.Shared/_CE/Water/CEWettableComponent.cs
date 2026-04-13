using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Water;

/// <summary>
/// Placed on entities that can become wet from water contact.
/// Subscribes to <see cref="CEWettedEvent"/> to apply wet stacks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEWettableComponent : Component
{
    /// <summary>
    /// Status effect prototype applied when this entity gets wet.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId StatusEffect = "CEStatusEffectWet";

    /// <summary>
    /// Default duration of each wet cycle if not overridden by the caller.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);
}
