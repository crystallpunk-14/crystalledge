using Robust.Shared.GameStates;

namespace Content.Shared._CE.Soul.Components;

/// <summary>
/// Marks a player as currently transferring souls into a receiver. Added by
/// <see cref="CESharedSoulSystem.TrySpendSouls"/>; removed once the animation duration
/// elapses, at which point the soul system raises <c>CESoulReceivedEvent</c> on the
/// receiver. While this component exists on a player, further <c>TrySpendSouls</c>
/// calls for that player are blocked.
/// Networked so the visual animation plays on every client that has the player in PVS.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CESharedSoulSystem))]
public sealed partial class CESoulTransferComponent : Component
{
    /// <summary>
    /// The receiver entity that the souls are being transferred into.
    /// </summary>
    [AutoNetworkedField]
    public NetEntity Receiver;

    /// <summary>
    /// Soul cost being transferred (informational; the souls are already deducted
    /// when this component is added).
    /// </summary>
    [AutoNetworkedField]
    public int Cost;

    /// <summary>
    /// Server time at which the transfer started. Animation progress is computed
    /// against this in client visuals.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan StartTime;

    /// <summary>
    /// Total animation duration in seconds. After this elapses on the server the
    /// transfer completes — <c>CESoulReceivedEvent</c> fires and the component is removed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Duration = 1.5f;
}
