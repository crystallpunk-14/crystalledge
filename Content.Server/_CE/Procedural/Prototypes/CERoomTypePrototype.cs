using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Prototypes;

/// <summary>
/// Describes a logical room type used in procedural dungeon generation.
/// Stores the room-filtering whitelist, connection behaviour and door overrides
/// for corridors that lead into rooms of this type.
/// </summary>
[Prototype("dungeonRoomType")]
public sealed partial class CERoomTypePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Whitelist used to filter <see cref="CEDungeonRoom3DPrototype"/> candidates
    /// when selecting a real room for this type.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// When <c>true</c>, rooms of this type may participate in <em>wide connections</em>
    /// — a direct floor-tile opening between two adjacent rooms with no corridor or doors.
    /// A wide connection is only used when <b>both</b> connected rooms support it;
    /// if either room has this set to <c>false</c>, a regular corridor with doors is used.
    /// </summary>
    [DataField]
    public bool SupportsWideConnection;

    /// <summary>
    /// Door prototype placed at the corridor endpoint that borders a room of this type.
    /// When <c>null</c>, falls back to <see cref="CEProceduralConfig.DoorPrototype"/>.
    /// </summary>
    [DataField]
    public EntProtoId? DoorPrototype;
}
