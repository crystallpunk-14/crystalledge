using Content.Server._CE.Procedural.Overview;
using Content.Shared._CE.Minimap;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Timing;
namespace Content.Server._CE.Minimap;

/// <summary>
/// Server-side tracker that fills <see cref="CEMinimapDataComponent.CurrentRoom"/> and
/// <see cref="CEMinimapDataComponent.VisitedRooms"/> for every entity carrying a minimap
/// component. The dungeon room data is looked up on the entity's current map; if the map
/// belongs to a z-level network we also scan its sibling maps so the minimap keeps working
/// when the player is on a floor that does not directly carry the dungeon graph.
/// </summary>
public sealed class CEMinimapSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<CEGeneratingProceduralDungeonComponent> _dungeonQuery;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        _dungeonQuery = GetEntityQuery<CEGeneratingProceduralDungeonComponent>();

        // When the player crosses between dungeon levels, the previous floor's room indices
        // become meaningless — wipe both the visited set and the current-room marker so the
        // minimap starts fresh on the new floor.
        SubscribeLocalEvent<CEMinimapDataComponent, CEDungeonPlayerLevelChangedEvent>(OnLevelChanged);
    }

    private void OnLevelChanged(Entity<CEMinimapDataComponent> ent, ref CEDungeonPlayerLevelChangedEvent args)
    {
        if (ent.Comp.VisitedRooms.Count == 0 && ent.Comp.CurrentRoom == null)
            return;

        ent.Comp.VisitedRooms.Clear();
        ent.Comp.CurrentRoom = null;
        Dirty(ent, ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<CEMinimapDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var minimap, out var xform))
        {
            UpdatePlayer((uid, minimap), xform);
        }
    }

    private void UpdatePlayer(Entity<CEMinimapDataComponent> ent, TransformComponent xform)
    {
        if (!TryFindDungeon(xform.MapUid, out var dungeon))
        {
            // Not in a procedural dungeon right now: clear the current room indicator
            // but keep the visited set (it'll be reset by the dungeon entry hook elsewhere
            // if/when one is added).
            if (ent.Comp.CurrentRoom != null)
            {
                ent.Comp.CurrentRoom = null;
                Dirty(ent);
            }
            return;
        }

        // Player position in world tiles (1 tile = 1 world unit).
        var worldPos = _transform.GetWorldPosition(xform);
        var tileX = (int) MathF.Floor(worldPos.X);
        var tileY = (int) MathF.Floor(worldPos.Y);

        var newRoom = FindRoomAt(dungeon, tileX, tileY);

        var changed = newRoom != null && ent.Comp.VisitedRooms.Add(newRoom.Value);

        if (ent.Comp.CurrentRoom != newRoom)
        {
            ent.Comp.CurrentRoom = newRoom;
            changed = true;
        }

        if (changed)
            Dirty(ent);
    }

    private static int? FindRoomAt(CEGeneratingProceduralDungeonComponent dungeon, int tileX, int tileY)
    {
        foreach (var room in dungeon.Rooms)
        {
            var minX = room.Position.X;
            var minY = room.Position.Y;
            var maxX = minX + room.Size.X;
            var maxY = minY + room.Size.Y;
            if (tileX >= minX && tileX < maxX && tileY >= minY && tileY < maxY)
                return room.Index;
        }

        return null;
    }

    private bool TryFindDungeon(EntityUid? mapUid, out CEGeneratingProceduralDungeonComponent dungeon)
    {
        dungeon = null!;

        if (mapUid is not { } currentMap)
            return false;

        // Direct hit: the map the player is on already carries the dungeon graph.
        if (_dungeonQuery.TryComp(currentMap, out var direct))
        {
            dungeon = direct;
            return true;
        }

        // Otherwise scan sibling maps in the same z-level network.
        if (!_zLevels.TryGetZNetwork(currentMap, out var zNet))
            return false;

        foreach (var sibling in zNet.Value.Comp.ZLevels.Values)
        {
            if (sibling is not { } siblingUid)
                continue;
            if (_dungeonQuery.TryComp(siblingUid, out var found))
            {
                dungeon = found;
                return true;
            }
        }

        return false;
    }
}
