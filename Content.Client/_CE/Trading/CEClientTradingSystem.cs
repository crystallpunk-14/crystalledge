using Content.Shared._CE.Trading.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client._CE.Trading;

public sealed partial class CEClientTradingSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CETradingSlotComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<CETradingSlotComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(Entity<CETradingSlotComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnAfterAutoHandleState(Entity<CETradingSlotComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<CETradingSlotComponent> ent)
    {
        if (ent.Comp.ActivePreviewProto is not { } proto)
        {
            _sprite.SetVisible(ent.Owner, false);
            return;
        }

        var temp = EntityManager.SpawnEntity(proto, MapCoordinates.Nullspace);
        _sprite.CopySprite(temp, ent.Owner);
        QueueDel(temp);
    }
}
