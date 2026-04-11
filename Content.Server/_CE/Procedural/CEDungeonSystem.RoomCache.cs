using Content.Shared._CE.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural;

/// <summary>
/// Partial responsible for caching passway (exit) information for each
/// <see cref="CEDungeonRoom3DPrototype"/>. The cache is built lazily the
/// first time it is requested and invalidated on prototype reloads.
/// </summary>
public sealed partial class CEDungeonSystem
{
    /// <summary>
    /// Cached passway data per room prototype ID.
    /// Built lazily by <see cref="EnsureRoomPasswayCache"/>.
    /// </summary>
    private Dictionary<string, List<RoomPassway>>? _roomPasswayCache;

    /// <summary>
    /// Ensures the passway cache is populated.
    /// Scans all <see cref="CEDungeonRoom3DPrototype"/> templates on first call.
    /// </summary>
    public void EnsureRoomPasswayCache()
    {
        if (_roomPasswayCache != null)
            return;

        _roomPasswayCache = new Dictionary<string, List<RoomPassway>>();

        foreach (var proto in _proto.EnumeratePrototypes<CEDungeonRoom3DPrototype>())
        {
            var passways = ScanRoomPassways(proto);
            _roomPasswayCache[proto.ID] = passways;
        }
    }

    /// <summary>
    /// Gets the cached passway list for a room prototype (unrotated).
    /// </summary>
    public List<RoomPassway> GetPassways(ProtoId<CEDungeonRoom3DPrototype> roomProtoId)
    {
        EnsureRoomPasswayCache();
        return _roomPasswayCache!.TryGetValue(roomProtoId, out var list) ? list : [];
    }

    /// <summary>
    /// Gets passway directions after applying a room rotation.
    /// Only returns the set of unique cardinal directions.
    /// </summary>
    public HashSet<Direction> GetPasswayDirections(ProtoId<CEDungeonRoom3DPrototype> roomProtoId, Angle rotation)
    {
        var passways = GetPassways(roomProtoId);
        var result = new HashSet<Direction>();

        foreach (var pw in passways)
        {
            var rotated = RotateDirection(pw.Direction, rotation);
            result.Add(rotated);
        }

        return result;
    }

    /// <summary>
    /// Checks whether a room prototype with a given rotation has exits in all
    /// of the <paramref name="requiredDirections"/>.
    /// </summary>
    public bool HasRequiredExits(
        ProtoId<CEDungeonRoom3DPrototype> roomProtoId,
        Angle rotation,
        HashSet<Direction> requiredDirections)
    {
        var available = GetPasswayDirections(roomProtoId, rotation);
        return requiredDirections.IsSubsetOf(available);
    }

    /// <summary>
    /// Scans a room prototype's template maps for <see cref="CERoomPasswayMarkerComponent"/>
    /// entities and records their direction, tile position, and z-level.
    /// </summary>
    private List<RoomPassway> ScanRoomPassways(CEDungeonRoom3DPrototype proto)
    {
        var passways = new List<RoomPassway>();

        if (!_proto.Resolve(proto.ZLevelMap, out var indexedZMap))
            return passways;

        for (var zLevel = 0; zLevel < proto.Height && zLevel < indexedZMap.Maps.Count; zLevel++)
        {
            var mapPath = indexedZMap.Maps[zLevel];
            var templateMapId = GetOrCreateTemplate(mapPath);
            var templateMapUid = _maps.GetMapOrInvalid(templateMapId);

            if (templateMapUid == EntityUid.Invalid)
                continue;

            var bounds = new Box2(proto.Offset, proto.Offset + proto.Size);

            foreach (var ent in _lookup.GetEntitiesIntersecting(templateMapUid, bounds, LookupFlags.Uncontained))
            {
                if (!HasComp<CERoomPasswayMarkerComponent>(ent))
                    continue;

                var xform = _xformQuery.GetComponent(ent);
                var direction = xform.LocalRotation.GetCardinalDir();
                var tilePos = xform.LocalPosition.Floored() - proto.Offset;

                passways.Add(new RoomPassway(direction, tilePos, zLevel));
            }
        }

        return passways;
    }

    /// <summary>
    /// Rotates a cardinal direction by the given angle.
    /// </summary>
    private static Direction RotateDirection(Direction dir, Angle rotation)
    {
        var dirAngle = dir.ToAngle() + rotation;
        return dirAngle.GetCardinalDir();
    }

    /// <summary>
    /// Invalidates the passway cache, forcing a rescan on next access.
    /// Should be called when prototypes are reloaded.
    /// </summary>
    public void InvalidateRoomPasswayCache()
    {
        _roomPasswayCache = null;
    }
}

/// <summary>
/// Describes one passway (exit) in a room template, in local (unrotated) coordinates.
/// </summary>
/// <param name="Direction">Cardinal direction the passway faces (outward from the room).</param>
/// <param name="TilePosition">Tile position within the room (relative to room origin, without offset).</param>
/// <param name="ZLevel">Which z-level layer of the room this passway is on (0-based).</param>
public readonly record struct RoomPassway(Direction Direction, Vector2i TilePosition, int ZLevel);
