using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.Procedural.GenerationSteps;

/// <summary>
/// Attaches one or more new rooms of the given type to randomly chosen existing rooms.
/// Each new room becomes a leaf node with a single connection to its parent.
/// </summary>
[DataDefinition]
public sealed partial class AppendRoom : CEDungeonGenerationStep
{
    /// <summary>
    /// Room type to assign to every appended room.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CERoomTypePrototype> RoomType;

    /// <summary>
    /// If set, only rooms whose current type is in this list may serve as attachment parents.
    /// <c>null</c> means any existing room is eligible.
    /// </summary>
    [DataField]
    public List<ProtoId<CERoomTypePrototype>>? AttachToTypes;

    /// <summary>
    /// How many rooms to append (picked randomly in range, inclusive).
    /// </summary>
    [DataField]
    public MinMax Count = new(1, 1);

    /// <inheritdoc/>
    public override Task Execute(CEGenerationStepContext context)
    {
        var comp = context.Comp;
        var count = context.Random.Next(Count.Min, Count.Max + 1);

        if (count <= 0)
            return Task.CompletedTask;

        var gridStep = context.MaxRoomSize + 1;
        var roomSize = new Vector2i(context.MaxRoomSize, context.MaxRoomSize);

        // Build occupied set.
        var occupied = new HashSet<Vector2i>(comp.Rooms.Count);
        foreach (var room in comp.Rooms)
        {
            occupied.Add(room.GridCoord);
        }

        // Candidate parents: rooms matching AttachToTypes (or any room if null/empty) with free neighbours.
        var candidates = new List<CEProceduralAbstractRoom>();
        foreach (var room in comp.Rooms)
        {
            if (AttachToTypes is { Count: > 0 } && (room.RoomType == null || !AttachToTypes.Contains(room.RoomType.Value)))
                continue;

            if (CEGenerationStepContext.HasEmptyNeighbor(room.GridCoord, occupied))
                candidates.Add(room);
        }

        // Shuffle to distribute attachments across the dungeon.
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = context.Random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        var added = 0;
        var candidateIdx = 0;
        while (added < count && candidateIdx < candidates.Count)
        {
            var parent = candidates[candidateIdx++];

            var freeNeighbors = new List<Vector2i>();
            foreach (var dir in CEGenerationStepContext.Directions)
            {
                var neighbor = parent.GridCoord + dir;
                if (!occupied.Contains(neighbor))
                    freeNeighbors.Add(neighbor);
            }

            if (freeNeighbors.Count == 0)
                continue;

            var chosenCoord = context.Random.Pick(freeNeighbors);

            var newRoom = new CEProceduralAbstractRoom
            {
                Index = comp.Rooms.Count,
                GridCoord = chosenCoord,
                Position = new Vector2i(chosenCoord.X * gridStep, chosenCoord.Y * gridStep),
                Size = roomSize,
                RoomType = RoomType,
            };

            comp.Rooms.Add(newRoom);
            comp.Connections.Add(new CEProceduralRoomConnection
            {
                RoomA = parent.Index,
                RoomB = newRoom.Index,
            });

            occupied.Add(chosenCoord);
            added++;

            // Allow the newly placed room to serve as a parent for subsequent rooms in this step.
            if (AttachToTypes == null || AttachToTypes.Count == 0 || AttachToTypes.Contains(RoomType))
                candidates.Add(newRoom);

            if (AttachToTypes == null || AttachToTypes.Count == 0 ||
                (parent.RoomType != null && AttachToTypes.Contains(parent.RoomType.Value)))
            {
                if (CEGenerationStepContext.HasEmptyNeighbor(parent.GridCoord, occupied))
                    candidates.Add(parent);
            }
        }

        if (added < count)
            context.Log.Warning($"AppendRoomStep could only attach {added}/{count} rooms of type {RoomType} — dungeon grid is too crowded.");

        return Task.CompletedTask;
    }
}
