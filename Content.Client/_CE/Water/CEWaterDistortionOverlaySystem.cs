using Robust.Client.Graphics;

namespace Content.Client._CE.Water;

/// <summary>
/// System responsible for rendering water distortion using <see cref="CEWaterDistortionOverlay"/>.
/// </summary>
public sealed class CEWaterDistortionOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlayMan.AddOverlay(new CEWaterDistortionOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<CEWaterDistortionOverlay>();
    }
}
