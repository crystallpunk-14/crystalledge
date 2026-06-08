using System.Threading.Tasks;
using Content.Shared._CE.Maths;
using Content.Shared._CE.Procedural;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural3D.GenerationSteps;

/// <summary>
/// Abstract base for a single step in a 3D procedural dungeon generation plan.
/// Steps operate purely on abstract graph data — no entity spawning — so no
/// cooperative yielding is needed.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEDungeonGenerationStep3D
{
    /// <param name="roomsByCoord">
    /// Grid-phase lookup: maps each room's <see cref="CEProceduralAbstractRoom3D.GridCoord"/>
    /// to its index in <see cref="CEGeneratingProceduralDungeon3DComponent.Rooms"/>.
    /// Steps that add rooms must update this dictionary.
    /// Valid only during grid-phase execution; not persisted after the plan completes.
    /// </param>
    public abstract Task Execute(
        CEGeneratingProceduralDungeon3DComponent comp,
        Dictionary<Vector3i, int> roomsByCoord,
        int maxRoomSize,
        int maxRoomHeight,
        IRobustRandom random,
        ISawmill log);

    /// <summary>
    /// Converts a logical grid coordinate to its world-tile XY origin.
    /// The Z component selects the z-level map and does not affect XY.
    /// </summary>
    protected static Vector2i GridToWorld(Vector3i gridCoord, int maxRoomSize)
    {
        var step = maxRoomSize + 1;
        return new Vector2i(gridCoord.X * step, gridCoord.Y * step);
    }

    /// <summary>
    /// Places or updates the room at <paramref name="gridCoord"/>.
    /// When <paramref name="roomType"/> is null, delegates to <see cref="EraseRoom"/> instead.
    /// </summary>
    /// <returns>
    /// Index of the room in <see cref="CEGeneratingProceduralDungeon3DComponent.Rooms"/>,
    /// or null when the room was erased.
    /// </returns>
    protected static int? SetRoom(
        CEGeneratingProceduralDungeon3DComponent comp,
        Dictionary<Vector3i, int> roomsByCoord,
        Vector3i gridCoord,
        int maxRoomSize,
        int maxRoomHeight,
        ProtoId<CERoomTypePrototype>? roomType)
    {
        if (roomType is null)
        {
            EraseRoom(comp, roomsByCoord, gridCoord);
            return null;
        }

        if (roomsByCoord.TryGetValue(gridCoord, out var index))
        {
            comp.Rooms[index].RoomType = roomType;
            return index;
        }

        var room = new CEProceduralAbstractRoom3D
        {
            GridCoord = gridCoord,
            Position = GridToWorld(gridCoord, maxRoomSize),
            Size = new Vector2i(maxRoomSize, maxRoomSize),
            Height = maxRoomHeight,
            RoomType = roomType,
        };
        var newIndex = comp.Rooms.Count;
        roomsByCoord[gridCoord] = newIndex;
        comp.Rooms.Add(room);
        return newIndex;
    }

    /// <summary>
    /// Removes the room at <paramref name="gridCoord"/> from the active grid, tombstones its list entry,
    /// and strips every connection that referenced it.
    /// Does nothing if no room exists at that coordinate.
    /// </summary>
    /// <remarks>
    /// The room's slot in <see cref="CEGeneratingProceduralDungeon3DComponent.Rooms"/> is kept
    /// (with <see cref="CEProceduralAbstractRoom3D.RoomType"/> set to null) so that all other
    /// rooms' list indices remain valid.
    /// </remarks>
    protected static void EraseRoom(
        CEGeneratingProceduralDungeon3DComponent comp,
        Dictionary<Vector3i, int> roomsByCoord,
        Vector3i gridCoord)
    {
        if (!roomsByCoord.TryGetValue(gridCoord, out var index))
            return;

        comp.Rooms[index].RoomType = null;
        roomsByCoord.Remove(gridCoord);
        comp.Connections.RemoveAll(c => c.RoomA == index || c.RoomB == index);
    }

    /// <summary>
    /// Connects the rooms at <paramref name="coordA"/> and <paramref name="coordB"/> by their list indices.
    /// Logs a warning and returns false if either room does not exist.
    /// </summary>
    protected static bool TryConnect(
        CEGeneratingProceduralDungeon3DComponent comp,
        Dictionary<Vector3i, int> roomsByCoord,
        Vector3i coordA,
        Vector3i coordB,
        ISawmill log)
    {
        if (!roomsByCoord.TryGetValue(coordA, out var indexA))
        {
            log.Warning($"TryConnect: no room at {coordA}");
            return false;
        }

        if (!roomsByCoord.TryGetValue(coordB, out var indexB))
        {
            log.Warning($"TryConnect: no room at {coordB}");
            return false;
        }

        comp.Connections.Add(new CEProceduralRoomConnection3D { RoomA = indexA, RoomB = indexB });
        return true;
    }
}
