using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Generators.Procedural;

/// <summary>
/// Partial: real prototype assignment — maps each abstract room to a concrete
/// <see cref="CEDungeonRoom3DPrototype"/>, applies rotation and centres the room in its grid cell.
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
                    maxSize: maxSizeVec,
                    roomType: room.RoomType);

                if (candidate == null)
                    break;

                // If no exits are required (isolated room), accept any room.
                if (required.Count == 0)
                {
                    roomProto = candidate;
                    chosenRotation = random.Next(4) * Math.PI / 2;
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
        {
            roomByIndex[room.Index] = room;
        }

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
}
