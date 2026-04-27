using Robust.Shared.GameStates;

namespace Content.Shared._CE.Minimap;

/// <summary>
/// Tracks per-player minimap state for the procedural dungeon the player is currently
/// exploring: which rooms they have already visited and which room they are in right now.
/// The minimap UI on the client only renders when this component is attached to the local
/// player AND the player's current map (via the z-level network) carries
/// <see cref="Procedural.CEGeneratingProceduralDungeonComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEMinimapDataComponent : Component
{
    /// <summary>
    /// Indices (in <c>CEGeneratingProceduralDungeonComponent.Rooms</c>) of all rooms the
    /// player has stepped into at least once during the current dungeon run.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<int> VisitedRooms = new();

    /// <summary>
    /// Index of the room the player is currently inside, or <c>null</c> if the player
    /// is between rooms (in a corridor / connection tile).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? CurrentRoom;

    public override bool SendOnlyToOwner => true;
}
