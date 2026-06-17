using Content.Shared._CE.WorldGen.Prototypes;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.WorldGen.Components;

/// <summary>
/// Tracks which named world locations a player has already entered, so the cinematic location popup
/// is only shown the first time each is visited. A location is identified by its chunk type. Also
/// remembers the location the player is currently in, used to detect crossing into a new one.
/// </summary>
[RegisterComponent]
public sealed partial class CELocationVisitorComponent : Component
{
    /// <summary>
    /// Localization keys of every location popup this player has already seen. Stored as
    /// <see cref="LocId"/> rather than a chunk-type id so several chunk types sharing the same title
    /// (sub-biomes) only ever announce once.
    /// </summary>
    [DataField]
    public HashSet<LocId> Visited = new();

    /// <summary>
    /// The chunk type the player was last resolved into; null until the player first stands on a
    /// painted chunk. Compared each tick to detect crossing into a new location.
    /// </summary>
    [DataField]
    public ProtoId<CEWorldChunkTypePrototype>? CurrentLocation;
}
