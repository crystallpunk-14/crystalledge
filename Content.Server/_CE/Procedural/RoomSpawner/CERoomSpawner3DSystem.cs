using Content.Server.Procedural;

namespace Content.Server._CE.Procedural.RoomSpawner;

public sealed class CERoomFill3DSystem : EntitySystem
{
    [Dependency] private readonly DungeonSystem _dungeon = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CERoomSpawner3DComponent, MapInitEvent>(OnRoomFillMapInit);
    }

    private void OnRoomFillMapInit(Entity<CERoomSpawner3DComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
    }
}
