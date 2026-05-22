using Content.Shared._CE.DPSMeter;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Timing;

namespace Content.Client._CE.DPSMeter;

/// <summary>
/// Registers and unregisters the <see cref="CEDPSMeterOverlay"/> with the overlay manager.
/// </summary>
public sealed class CEDPSMeterOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private CEDPSMeterOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CEDPSMeterOverlay(EntityManager, _cache, _timing);
        _overlayMan.AddOverlay(_overlay);

        SubscribeLocalEvent<CEDPSMeterComponent, ComponentRemove>(OnMeterRemoved);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnMeterRemoved(Entity<CEDPSMeterComponent> ent, ref ComponentRemove args)
    {
        _overlay.ClearCache(ent);
    }
}
