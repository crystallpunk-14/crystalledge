using System.Numerics;
using Content.Shared._CE.Procedural;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: spawning room prototypes onto the map grid and placing perimeter walls.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem
{
    /// <summary>
    /// 8-directional offsets (cardinals + diagonals) for perimeter detection.
    /// </summary>
    private static readonly Vector2i[] AllNeighbors =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    ];

    /// <summary>
    /// Places wall entities around the perimeter of all reserved (occupied) tiles.
    /// A wall is placed at every neighbouring position (8-directional, including diagonals)
    /// that is not itself a reserved tile. Walls are spawned on every z-level in the network.
    /// </summary>
    private void PlaceWalls(
        CEProceduralConfig config,
        EntityUid baseMapUid,
        Dictionary<EntityUid, int> mapsByDepth,
        HashSet<Vector2i> reservedTiles)
    {
        // Compute wall positions: neighbours of reserved tiles that are not occupied.
        var wallPositions = new HashSet<Vector2i>();

        foreach (var tile in reservedTiles)
        {
            foreach (var offset in AllNeighbors)
            {
                var neighbor = tile + offset;
                if (!reservedTiles.Contains(neighbor))
                    wallPositions.Add(neighbor);
            }
        }

        if (wallPositions.Count == 0)
            return;

        // Spawn walls on every z-level.
        foreach (var (levelMapUid, _) in mapsByDepth)
        {
            foreach (var pos in wallPositions)
            {
                // Tile center = (tileX + 0.5, tileY + 0.5).
                var worldPos = new Vector2(pos.X + 0.5f, pos.Y + 0.5f);
                Spawn(config.WallPrototype, new EntityCoordinates(levelMapUid, worldPos));
            }
        }
    }

    /// <summary>
    /// Spawns a 3D room prototype for each abstract room that has a valid <see cref="CEProceduralAbstractRoom.RoomProtoId"/>.
    /// The room is placed at the room's <see cref="CEProceduralAbstractRoom.Position"/> with
    /// the pre-computed <see cref="CEProceduralAbstractRoom.Rotation"/>.
    /// </summary>
    private void SpawnRooms(
        CEGeneratingProceduralDungeonComponent comp,
        EntityUid gridUid,
        MapGridComponent grid,
        Random random,
        HashSet<Vector2i> reservedTiles)
    {

        foreach (var room in comp.Rooms)
        {
            if (room.RoomProtoId == null)
                continue;

            if (!_proto.TryIndex(room.RoomProtoId.Value, out var roomProto))
            {
                Log.Warning($"CEProceduralGeneratorSystem: unknown room prototype '{room.RoomProtoId}'.");
                continue;
            }

            // Build the transform: translate to the unrotated room origin, then rotate.
            // room.Position is the top-left of the EFFECTIVE (rotated) bounding box.
            // The transform expects the top-left of the UNROTATED prototype.
            // Both share the same center:
            //   room.Position + effectiveSize/2 == unrotatedOrigin + protoSize/2
            var center = new Vector2(
                room.Position.X + room.Size.X / 2f,
                room.Position.Y + room.Size.Y / 2f);
            var unrotatedOrigin = center - (Vector2)roomProto.Size / 2f;

            var originTransform = Matrix3Helpers.CreateTranslation(unrotatedOrigin.X, unrotatedOrigin.Y);
            var roomTransform = Matrix3Helpers.CreateTransform((Vector2)roomProto.Size / 2f, room.Rotation);
            var finalTransform = Matrix3x2.Multiply(roomTransform, originTransform);

            if (!_dungeon.TrySpawn3DRoom(gridUid, grid, finalTransform, roomProto, reservedTiles))
            {
                Log.Warning($"CEProceduralGeneratorSystem: failed to spawn room {room.Index} (proto '{room.RoomProtoId}').");
                continue;
            }

            // After the room is fully spawned, mark its tile positions as reserved
            // so future rooms don't overwrite them.
            var roomCenter = (roomProto.Offset + roomProto.Size / 2f) * grid.TileSize;
            var tileOffset = -roomCenter + grid.TileSizeHalfVector;

            for (var x = 0; x < roomProto.Size.X; x++)
            {
                for (var y = 0; y < roomProto.Size.Y; y++)
                {
                    var indices = new Vector2i(x + roomProto.Offset.X, y + roomProto.Offset.Y);
                    var tilePos = Vector2.Transform(indices + tileOffset, finalTransform);
                    reservedTiles.Add(tilePos.Floored());
                }
            }
        }
    }
}
