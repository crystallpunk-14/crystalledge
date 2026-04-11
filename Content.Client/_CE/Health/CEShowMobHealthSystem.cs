using Content.Client.Overlays;
using Content.Shared._CE.Health.Components;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client._CE.Health;

public sealed class CEShowMobHealthSystem : EquipmentHudSystem<CEShowMobHealthComponent>
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private CEEntityHealthBarOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CEEntityHealthBarOverlay(EntityManager);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<CEShowMobHealthComponent> args)
    {
        base.UpdateInternal(args);

        if (!_overlayMan.HasOverlay<CEEntityHealthBarOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        _overlayMan.RemoveOverlay(_overlay);
    }
}
