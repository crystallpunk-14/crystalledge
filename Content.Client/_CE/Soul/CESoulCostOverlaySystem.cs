using Content.Shared._CE.Soul.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;

namespace Content.Client._CE.Soul;

/// <summary>
/// Owns the lifecycle of <see cref="CESoulCostOverlay"/>: the overlay does the
/// per-frame drawing of soul-cost labels above receivers, this system just
/// registers/unregisters it with the overlay manager.
/// </summary>
public sealed class CESoulCostOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private CESoulCostOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CESoulCostOverlay(EntityManager, _player, _cache);
        _overlayMan.AddOverlay(_overlay);

        SubscribeLocalEvent<CESoulReceiverComponent, ComponentRemove>(OnReceiverRemoved);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnReceiverRemoved(Entity<CESoulReceiverComponent> ent, ref ComponentRemove args)
    {
        _overlay.ClearCache(ent);
    }
}
