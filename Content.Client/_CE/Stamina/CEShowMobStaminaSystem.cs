using Content.Client.Overlays;
using Content.Shared._CE.Stamina;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client._CE.Stamina;

public sealed class CEShowMobStaminaSystem : EquipmentHudSystem<CEShowMobStaminaComponent>
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private CEEntityStaminaBarOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CEEntityStaminaBarOverlay(EntityManager);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<CEShowMobStaminaComponent> args)
    {
        base.UpdateInternal(args);

        if (!_overlayMan.HasOverlay<CEEntityStaminaBarOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        _overlayMan.RemoveOverlay(_overlay);
    }
}
