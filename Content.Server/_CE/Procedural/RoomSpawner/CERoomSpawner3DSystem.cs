using Robust.Shared.Map.Components;

namespace Content.Server._CE.Procedural.RoomSpawner;

public sealed class CERoomFill3DSystem : EntitySystem
{
    [Dependency] private readonly CEDungeonSystem _dungeon = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CERoomSpawner3DComponent, MapInitEvent>(OnRoomFillMapInit);
    }

    private void OnRoomFillMapInit(Entity<CERoomSpawner3DComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);

        if (xform.GridUid != null)
        {
            var random = new Random();
            var room = _dungeon.GetRoomPrototype(random, ent.Comp.RoomWhitelist, ent.Comp.MinSize, ent.Comp.MaxSize);

            if (room != null)
            {
                var mapGrid = Comp<MapGridComponent>(xform.GridUid.Value);
                _dungeon.TrySpawn3DRoom(
                    xform.GridUid.Value,
                    mapGrid,
                    _maps.LocalToTile(xform.GridUid.Value, mapGrid, xform.Coordinates) - new Vector2i(room.Size.X/2,room.Size.Y/2),
                    room,
                    random,
                    null,
                    clearExisting: ent.Comp.ClearExisting,
                    rotation: ent.Comp.Rotation);
            }
            else
            {
                Log.Error($"Unable to find matching room prototype for {ToPrettyString(ent)}");
            }
        }

        // Final cleanup
        QueueDel(ent);
    }
}
