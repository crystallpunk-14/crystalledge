using Content.Client.Overlays;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client._CE.Mana;

public sealed class CEShowMobManaSystem : EquipmentHudSystem<CEShowMobManaComponent>
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private CEEntityManaBarOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CEEntityManaBarOverlay(EntityManager);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<CEShowMobManaComponent> args)
    {
        base.UpdateInternal(args);

        if (!_overlayMan.HasOverlay<CEEntityManaBarOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        _overlayMan.RemoveOverlay(_overlay);
    }
}
