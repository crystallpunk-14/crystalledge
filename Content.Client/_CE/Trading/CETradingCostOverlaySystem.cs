using Content.Shared._CE.Trading.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;

namespace Content.Client._CE.Trading;

public sealed partial class CETradingCostOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IPlayerManager _player = default!;

    private CETradingCostOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CETradingCostOverlay(EntityManager, _player, _cache);
        _overlayMan.AddOverlay(_overlay);

        SubscribeLocalEvent<CETradingSlotComponent, ComponentRemove>(OnSlotRemoved);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnSlotRemoved(Entity<CETradingSlotComponent> ent, ref ComponentRemove args)
    {
        _overlay.ClearCache(ent);
    }
}
