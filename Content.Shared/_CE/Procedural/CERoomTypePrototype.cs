using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Procedural;

/// <summary>
/// Describes a logical room type used in procedural dungeon generation.
/// Stores the room-filtering whitelist, connection behaviour, door overrides,
/// debug overlay colours and minimap rendering data.
/// </summary>
[Prototype("dungeonRoomType")]
public sealed partial class CERoomTypePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

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
    /// When <c>null</c>, falls back to the generator config's default door prototype.
    /// </summary>
    [DataField]
    public EntProtoId? DoorPrototype;

    /// <summary>
    /// Icon displayed on the minimap for rooms of this type.
    /// Rendered when the room is visited or (if <see cref="RevealedFromNeighbour"/> is true) previewed.
    /// </summary>
    [DataField]
    public SpriteSpecifier? MinimapIcon;

    /// <summary>
    /// Fill colour used for this room type in the procedural generation debug overlay.
    /// </summary>
    [DataField]
    public Color DebugFillColor = Color.Gray.WithAlpha(0.08f);

    /// <summary>
    /// Border colour used for this room type in the procedural generation debug overlay.
    /// </summary>
    [DataField]
    public Color DebugBorderColor = Color.Gray.WithAlpha(0.8f);

    /// <summary>
    /// When <c>true</c>, the minimap icon for this room type is revealed even when the room is
    /// only a preview (adjacent to a visited room but not yet entered).
    /// When <c>false</c>, the icon is hidden until the room itself is visited.
    /// </summary>
    [DataField]
    public bool RevealedFromNeighbour;
}
