using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Soul.Components;

/// <summary>
/// Marks an entity as a "soul sink" that can consume a fixed amount of souls
/// from a player when something asks the soul system to do so.
/// Pure data — has no behaviour of its own. Consumer systems (e.g. blessing statue)
/// call <see cref="CESharedSoulSystem.TrySpendSouls"/> from their own interaction
/// handlers and listen for <see cref="CESoulSpentEvent"/> on this entity.
///
/// Concurrency / locking (one player at a time, queueing, …) is the responsibility
/// of the consumer system, not of this component or the soul system.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CESharedSoulSystem))]
public sealed partial class CESoulReceiverComponent : Component
{
    /// <summary>
    /// Soul cost charged per successful interaction.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Cost = 25;

    /// <summary>
    /// Screen-space offset (in tile units) applied to the floating soul cost label so it can
    /// be lifted above tall sprites such as statues. X is screen-right, Y is screen-up;
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 Offset = Vector2.Zero;
}
