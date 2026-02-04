using Content.Shared._CE.Procedural;
using Content.Shared.Procedural;
using Content.Shared.Whitelist;

namespace Content.Server._CE.Procedural;

public sealed partial class CEDungeonSystem
{
    private readonly List<CEDungeonRoom3DPrototype> _availableRooms = new();

    private void InitializeRooms()
    {

    }


    /// <summary>
    /// Gets a random dungeon room matching the specified area, whitelist and size.
    /// </summary>
    public CEDungeonRoom3DPrototype? GetRoomPrototype(Vector2i size, Random random, EntityWhitelist? whitelist = null)
    {
        return GetRoomPrototype(random, whitelist, minSize: size, maxSize: size);
    }


    /// <summary>
    /// Gets a random dungeon room matching the specified area and whitelist and size range
    /// </summary>
    public CEDungeonRoom3DPrototype? GetRoomPrototype(Random random,
        EntityWhitelist? whitelist = null,
        Vector2i? minSize = null,
        Vector2i? maxSize = null)
    {
        // Can never be true.
        if (whitelist is { Tags: null })
        {
            return null;
        }

        _availableRooms.Clear();

        foreach (var proto in _proto.EnumeratePrototypes<CEDungeonRoom3DPrototype>())
        {
            if (minSize is not null && (proto.Size.X < minSize.Value.X || proto.Size.Y < minSize.Value.Y))
                continue;

            if (maxSize is not null && (proto.Size.X > maxSize.Value.X || proto.Size.Y > maxSize.Value.Y))
                continue;

            if (whitelist == null)
            {
                _availableRooms.Add(proto);
                continue;
            }

            foreach (var tag in whitelist.Tags)
            {
                if (!proto.Tags.Contains(tag))
                    continue;

                _availableRooms.Add(proto);
                break;
            }
        }

        if (_availableRooms.Count == 0)
            return null;

        var room = _availableRooms[random.Next(_availableRooms.Count)];

        return room;
    }
}
