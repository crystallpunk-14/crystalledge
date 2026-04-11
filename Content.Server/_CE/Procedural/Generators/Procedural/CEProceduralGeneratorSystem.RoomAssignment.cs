using System.Threading.Tasks;
using Content.Shared._CE.Procedural;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: room type assignment (Exit, Entrance, Blessing, DeadEnd)
/// and real room prototype selection.
/// </summary>
public sealed partial class CEProceduralGeneratorSystem
{
    /// <summary>
    /// For each abstract room, selects a random real <see cref="CEDungeonRoom3DPrototype"/>
    /// that fits within MaxRoomSize, chooses a rotation that satisfies the required exit
    /// directions (based on neighbour connections), shrinks the abstract room to the
    /// real room's size, and centres it within the original grid cell.
    /// Uses the whitelist from the room's type-specific pack.
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
            var pack = GetPackForType(config, room.RoomType);

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
                    pack.Whitelist,
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
                ShuffleArray(candidateRotations, random);

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
    /// Fisher–Yates shuffle for a small array.
    /// </summary>
    private static void ShuffleArray<T>(T[] array, Random random)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    /// <summary>
    /// Returns the <see cref="CEProceduralRoomPack"/> matching the given room type.
    /// </summary>
    private static CEProceduralRoomPack GetPackForType(CEProceduralConfig config, CEProceduralRoomType type)
    {
        return type switch
        {
            CEProceduralRoomType.Exit => config.ExitRoom,
            CEProceduralRoomType.Entrance => config.EntranceRooms,
            CEProceduralRoomType.Blessing => config.BlessingRooms,
            CEProceduralRoomType.DeadEnd => config.DeadEndRooms,
            _ => config.GeneralRooms,
        };
    }

    /// <summary>
    /// Assigns special room types after the graph is built.
    /// <list type="bullet">
    ///   <item>Exit: room at grid (0,0).</item>
    ///   <item>Entrances: dead-ends (1 connection), picked maximally far apart.</item>
    ///   <item>Blessings: remaining dead-ends, picked maximally far apart.</item>
    ///   <item>DeadEnd: all remaining dead-end rooms.</item>
    ///   <item>All other rooms remain General.</item>
    /// </list>
    /// </summary>
    internal void AssignRoomTypes(CEGeneratingProceduralDungeonComponent comp, CEProceduralConfig config)
    {
        // Count connections per room.
        var connectionCount = new Dictionary<int, int>();
        foreach (var conn in comp.Connections)
        {
            connectionCount[conn.RoomA] = connectionCount.GetValueOrDefault(conn.RoomA) + 1;
            connectionCount[conn.RoomB] = connectionCount.GetValueOrDefault(conn.RoomB) + 1;
        }

        // 1. Exit at (0, 0).
        foreach (var room in comp.Rooms)
        {
            if (room.GridCoord == Vector2i.Zero)
            {
                room.RoomType = CEProceduralRoomType.Exit;
                break;
            }
        }

        // Collect dead-ends (rooms with exactly 1 connection), excluding the exit.
        var deadEnds = new List<CEProceduralAbstractRoom>();
        foreach (var room in comp.Rooms)
        {
            if (room.RoomType != CEProceduralRoomType.General)
                continue;

            if (connectionCount.GetValueOrDefault(room.Index) == 1)
                deadEnds.Add(room);
        }

        // 2. Entrances: pick dead-ends farthest from center first, then far apart.
        var entranceCount = _random.Next(
            config.EntranceCount.Min,
            config.EntranceCount.Max + 1);
        PickFarFromCenterThenApart(deadEnds, CEProceduralRoomType.Entrance, entranceCount);

        // Remove assigned rooms from dead-end pool.
        deadEnds.RemoveAll(r => r.RoomType != CEProceduralRoomType.General);

        // 3. Blessings: pick from remaining dead-ends, maximally far apart.
        var blessingCount = _random.Next(
            config.BlessingCount.Min,
            config.BlessingCount.Max + 1);
        PickFarApart(deadEnds, CEProceduralRoomType.Blessing, blessingCount);

        // Remove assigned rooms from dead-end pool.
        deadEnds.RemoveAll(r => r.RoomType != CEProceduralRoomType.General);

        // 4. Dead-ends: all remaining dead-end rooms get the DeadEnd type.
        foreach (var room in deadEnds)
        {
            room.RoomType = CEProceduralRoomType.DeadEnd;
        }
    }

    /// <summary>
    /// Greedily picks rooms from <paramref name="candidates"/> that are maximally far apart
    /// from already-picked rooms and assigns them the given <paramref name="type"/>.
    /// Uses grid-coordinate Manhattan distance.
    /// </summary>
    private static void PickFarApart(
        List<CEProceduralAbstractRoom> candidates,
        CEProceduralRoomType type,
        int count)
    {
        if (count <= 0 || candidates.Count == 0)
            return;

        var picked = new List<CEProceduralAbstractRoom>();

        for (var n = 0; n < count && candidates.Count > 0; n++)
        {
            CEProceduralAbstractRoom? best = null;
            var bestMinDist = -1;

            foreach (var candidate in candidates)
            {
                if (candidate.RoomType != CEProceduralRoomType.General)
                    continue;

                // Minimum Manhattan distance to all already picked rooms.
                var minDist = int.MaxValue;
                foreach (var p in picked)
                {
                    var dist = Math.Abs(candidate.GridCoord.X - p.GridCoord.X)
                               + Math.Abs(candidate.GridCoord.Y - p.GridCoord.Y);
                    if (dist < minDist)
                        minDist = dist;
                }

                // First pick: use MaxValue so any candidate wins.
                if (picked.Count == 0)
                    minDist = int.MaxValue;

                if (minDist > bestMinDist)
                {
                    bestMinDist = minDist;
                    best = candidate;
                }
            }

            if (best == null)
                break;

            best.RoomType = type;
            picked.Add(best);
        }
    }

    /// <summary>
    /// Greedily picks rooms that are (1) farthest from the dungeon center (grid origin)
    /// and (2) as a tiebreaker, farthest from already-picked rooms.
    /// Uses grid-coordinate Manhattan distance.
    /// </summary>
    private static void PickFarFromCenterThenApart(
        List<CEProceduralAbstractRoom> candidates,
        CEProceduralRoomType type,
        int count)
    {
        if (count <= 0 || candidates.Count == 0)
            return;

        var picked = new List<CEProceduralAbstractRoom>();

        for (var n = 0; n < count && candidates.Count > 0; n++)
        {
            CEProceduralAbstractRoom? best = null;
            var bestCenterDist = -1;
            var bestMinPeerDist = -1;

            foreach (var candidate in candidates)
            {
                if (candidate.RoomType != CEProceduralRoomType.General)
                    continue;

                // Primary: Manhattan distance from the dungeon center (0, 0).
                var centerDist = Math.Abs(candidate.GridCoord.X) + Math.Abs(candidate.GridCoord.Y);

                // Secondary: minimum Manhattan distance to all already-picked rooms.
                var minPeerDist = int.MaxValue;
                foreach (var p in picked)
                {
                    var dist = Math.Abs(candidate.GridCoord.X - p.GridCoord.X)
                               + Math.Abs(candidate.GridCoord.Y - p.GridCoord.Y);
                    if (dist < minPeerDist)
                        minPeerDist = dist;
                }

                // First pick — no peers, so peer distance is irrelevant.
                if (picked.Count == 0)
                    minPeerDist = int.MaxValue;

                // Compare: primary wins, secondary is tiebreaker.
                if (centerDist > bestCenterDist
                    || (centerDist == bestCenterDist && minPeerDist > bestMinPeerDist))
                {
                    bestCenterDist = centerDist;
                    bestMinPeerDist = minPeerDist;
                    best = candidate;
                }
            }

            if (best == null)
                break;

            best.RoomType = type;
            picked.Add(best);
        }
    }
}
