using System.Threading.Tasks;
using Content.Server._CE.Procedural.Prototypes;
using Content.Shared._CE.Procedural;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: room type assignment, special room graph expansion, and real prototype selection.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem
{
    /// <summary>
    /// For each abstract room, selects a random real <see cref="CEDungeonRoom3DPrototype"/>
    /// that fits within MaxRoomSize, chooses a rotation that satisfies the required exit
    /// directions (based on neighbour connections), shrinks the abstract room to the
    /// real room's size, and centres it within the original grid cell.
    /// Uses the whitelist from the room's type-specific prototype.
    /// </summary>
    internal async Task AssignRealRooms(CEGeneratingProceduralDungeonComponent comp, CEProceduralConfig config, Func<ValueTask> suspend)
    {
        var maxSize = config.MaxRoomSize;
        var step = maxSize + 1;
        var random = new Random(_random.Next());
        var maxSizeVec = new Vector2i(maxSize, maxSize);

        // Ensure the passway cache is built before we start checking exits.
        _dungeon.EnsureRoomPasswayCache();

        // Build a map of required exit directions per room index.
        // For each room, the required exits are the directions toward its graph neighbours.
        var requiredExits = BuildRequiredExitsMap(comp);

        // Candidate rotations (0°, 90°, 180°, 270°).
        var candidateRotations = new[] { Angle.Zero, new Angle(Math.PI / 2), new Angle(Math.PI), new Angle(3 * Math.PI / 2) };

        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            // Yield every 10 rooms — each room tries up to 50 prototypes.
            if (i > 0 && i % 10 == 0)
                await suspend();

            var room = comp.Rooms[i];

            // Pick the whitelist based on the room's assigned type.
            var roomTypeProto = GetRoomTypeProto(config, room.RoomType);

            // Determine required exit directions for this room.
            var required = requiredExits.GetValueOrDefault(room.Index) ?? new HashSet<Direction>();

            // Try multiple times to find a valid prototype + rotation combo.
            const int maxAttempts = 50;
            CEDungeonRoom3DPrototype? roomProto = null;
            var chosenRotation = Angle.Zero;
            var found = false;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = _dungeon.GetRoomPrototype(
                    random,
                    roomTypeProto?.Whitelist,
                    maxSize: maxSizeVec);

                if (candidate == null)
                    break;

                // If no exits are required (isolated room), accept any room.
                if (required.Count == 0)
                {
                    roomProto = candidate;
                    chosenRotation = _dungeon.GetRoomRotation(candidate, random);
                    found = true;
                    break;
                }

                // Try each of the 4 cardinal rotations to see if one satisfies all required exits.
                // Shuffle the order so results are not biased toward 0°.
                for (var s = candidateRotations.Length - 1; s > 0; s--)
                {
                    var t = random.Next(s + 1);
                    (candidateRotations[s], candidateRotations[t]) = (candidateRotations[t], candidateRotations[s]);
                }

                foreach (var rot in candidateRotations)
                {
                    if (_dungeon.HasRequiredExits(candidate.ID, rot, required))
                    {
                        roomProto = candidate;
                        chosenRotation = rot;
                        found = true;
                        break;
                    }
                }

                if (found)
                    break;
            }

            if (roomProto == null)
            {
                Log.Error($"CEProceduralGeneratorSystem: no matching room prototype found for abstract room #{i} (type={room.RoomType}).");
                continue;
            }

            room.RoomProtoId = roomProto.ID;
            room.Rotation = chosenRotation;

            // Calculate effective size after rotation.
            // 90 / 270 degrees swap width and height.
            var isRotated90 = Math.Abs(room.Rotation.Theta - Math.PI / 2) < 0.01
                              || Math.Abs(room.Rotation.Theta - 3 * Math.PI / 2) < 0.01;

            var effectiveSize = isRotated90
                ? new Vector2i(roomProto.Size.Y, roomProto.Size.X)
                : roomProto.Size;

            // Shrink abstract room to match the real room's effective size.
            room.Size = effectiveSize;

            // Center the room within the original grid cell.
            // The cell origin is gridCoord * step and has maxSize × maxSize space.
            var cellOrigin = new Vector2i(room.GridCoord.X * step, room.GridCoord.Y * step);
            var slack = new Vector2i(
                Math.Max(0, maxSize - effectiveSize.X),
                Math.Max(0, maxSize - effectiveSize.Y));

            var offsetX = slack.X / 2;
            var offsetY = slack.Y / 2;

            room.Position = new Vector2i(cellOrigin.X + offsetX, cellOrigin.Y + offsetY);
        }
    }

    /// <summary>
    /// Builds a map from room index to the set of cardinal directions where the room
    /// must have exits (toward its graph neighbours).
    /// </summary>
    private static Dictionary<int, HashSet<Direction>> BuildRequiredExitsMap(
        CEGeneratingProceduralDungeonComponent comp)
    {
        // Index rooms by their index for GridCoord lookup.
        var roomByIndex = new Dictionary<int, CEProceduralAbstractRoom>();
        foreach (var room in comp.Rooms)
            roomByIndex[room.Index] = room;

        var result = new Dictionary<int, HashSet<Direction>>();

        foreach (var conn in comp.Connections)
        {
            if (!roomByIndex.TryGetValue(conn.RoomA, out var roomA) ||
                !roomByIndex.TryGetValue(conn.RoomB, out var roomB))
                continue;

            var dirAtoB = GridCoordToDirection(roomB.GridCoord - roomA.GridCoord);
            var dirBtoA = GridCoordToDirection(roomA.GridCoord - roomB.GridCoord);

            if (dirAtoB != Direction.Invalid)
            {
                if (!result.TryGetValue(conn.RoomA, out var setA))
                {
                    setA = new HashSet<Direction>();
                    result[conn.RoomA] = setA;
                }
                setA.Add(dirAtoB);
            }

            if (dirBtoA != Direction.Invalid)
            {
                if (!result.TryGetValue(conn.RoomB, out var setB))
                {
                    setB = new HashSet<Direction>();
                    result[conn.RoomB] = setB;
                }
                setB.Add(dirBtoA);
            }
        }

        return result;
    }

    /// <summary>
    /// Converts a grid-coordinate delta (adjacent cell) to a cardinal direction.
    /// </summary>
    private static Direction GridCoordToDirection(Vector2i delta)
    {
        return delta switch
        {
            { X: > 0 } => Direction.East,
            { X: < 0 } => Direction.West,
            { Y: > 0 } => Direction.North,
            { Y: < 0 } => Direction.South,
            _ => Direction.Invalid,
        };
    }

    /// <summary>
    /// Returns the <see cref="CERoomTypePrototype"/> for the given room type, or <c>null</c> if none is configured.
    /// </summary>
    private CERoomTypePrototype? GetRoomTypeProto(CEProceduralConfig config, CEProceduralRoomType type)
    {
        var protoId = type switch
        {
            CEProceduralRoomType.Exit => config.ExitRoom,
            CEProceduralRoomType.Entrance => config.EntranceRooms,
            CEProceduralRoomType.Blessing => config.BlessingRooms,
            CEProceduralRoomType.Treasure => config.TreasureRooms,
            CEProceduralRoomType.DeadEnd => config.DeadEndRooms,
            _ => config.GeneralRooms,
        };

        if (protoId == null)
            return null;

        _proto.TryIndex(protoId.Value, out var proto);
        return proto;
    }

    /// <summary>
    /// Marks the room at grid (0,0) as <see cref="CEProceduralRoomType.Exit"/>.
    /// Call this before <see cref="AppendSpecialRooms"/> so that corridor rooms are still
    /// <see cref="CEProceduralRoomType.General"/> and available as parents for special rooms.
    /// </summary>
    internal void AssignExitRoom(CEGeneratingProceduralDungeonComponent comp)
    {
        foreach (var room in comp.Rooms)
        {
            if (room.GridCoord == Vector2i.Zero)
            {
                room.RoomType = CEProceduralRoomType.Exit;
                break;
            }
        }
    }

    /// <summary>
    /// Marks all <see cref="CEProceduralRoomType.General"/> rooms that have exactly one
    /// connection as <see cref="CEProceduralRoomType.DeadEnd"/>.
    /// Call this <em>after</em> <see cref="AppendSpecialRooms"/> so that special rooms
    /// are attached to corridors first, and only the truly unassigned leaf rooms become
    /// dead-ends.
    /// </summary>
    internal void AssignDeadEnds(CEGeneratingProceduralDungeonComponent comp)
    {
        // Count connections per room.
        var connectionCount = new Dictionary<int, int>();
        foreach (var conn in comp.Connections)
        {
            connectionCount[conn.RoomA] = connectionCount.GetValueOrDefault(conn.RoomA) + 1;
            connectionCount[conn.RoomB] = connectionCount.GetValueOrDefault(conn.RoomB) + 1;
        }

        foreach (var room in comp.Rooms)
        {
            if (room.RoomType != CEProceduralRoomType.General)
                continue;

            if (connectionCount.GetValueOrDefault(room.Index) == 1)
                room.RoomType = CEProceduralRoomType.DeadEnd;
        }
    }

    /// <summary>
    /// Appends <paramref name="count"/> new rooms of the given <paramref name="type"/> to
    /// the dungeon graph, each attached to a random <see cref="CEProceduralRoomType.General"/>
    /// (corridor) room that still has at least one free cardinal grid cell.
    /// <para>
    /// Call this <em>before</em> <see cref="AssignDeadEnds"/> so that corridor rooms are
    /// still <see cref="CEProceduralRoomType.General"/> and available as parents.
    /// </para>
    /// </summary>
    internal void AppendSpecialRooms(
        CEGeneratingProceduralDungeonComponent comp,
        int count,
        CEProceduralRoomType type,
        int maxRoomSize)
    {
        if (count <= 0)
            return;

        var step = maxRoomSize + 1;
        var roomSize = new Vector2i(maxRoomSize, maxRoomSize);

        // Build the current occupied grid coordinate set.
        var occupied = new HashSet<Vector2i>();
        foreach (var room in comp.Rooms)
            occupied.Add(room.GridCoord);

        // Candidate parents: General corridor rooms with at least one free cardinal neighbor.
        var candidates = new List<CEProceduralAbstractRoom>();
        foreach (var room in comp.Rooms)
        {
            if (room.RoomType != CEProceduralRoomType.General)
                continue;

            if (HasEmptyNeighbor(room.GridCoord, occupied))
                candidates.Add(room);
        }

        // Shuffle so we distribute across the dungeon.
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        var added = 0;
        foreach (var parent in candidates)
        {
            if (added >= count)
                break;

            // Collect free cardinal neighbors at this point in time (prior iterations may have filled some).
            var freeNeighbors = new List<Vector2i>();
            foreach (var dir in Directions)
            {
                var neighbor = parent.GridCoord + dir;
                if (!occupied.Contains(neighbor))
                    freeNeighbors.Add(neighbor);
            }

            if (freeNeighbors.Count == 0)
                continue;

            var chosenCoord = _random.Pick(freeNeighbors);

            var newRoom = new CEProceduralAbstractRoom
            {
                Index = comp.Rooms.Count,
                GridCoord = chosenCoord,
                Position = new Vector2i(chosenCoord.X * step, chosenCoord.Y * step),
                Size = roomSize,
                RoomType = type,
            };

            comp.Rooms.Add(newRoom);
            comp.Connections.Add(new CEProceduralRoomConnection
            {
                RoomA = parent.Index,
                RoomB = newRoom.Index,
            });

            occupied.Add(chosenCoord);
            added++;
        }

        if (added < count)
        {
            Log.Warning($"CEProceduralGeneratorSystem: could only append {added}/{count} rooms of type {type} — dungeon grid is too crowded.");
        }
    }
}
