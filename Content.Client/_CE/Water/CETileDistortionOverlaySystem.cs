using Robust.Client.Graphics;

namespace Content.Client._CE.Water;

/// <summary>
/// System responsible for rendering tile distortion using <see cref="CETileDistortionOverlay"/>.
/// </summary>
public sealed class CETileDistortionOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlayMan.AddOverlay(new CETileDistortionOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<CETileDistortionOverlay>();
    }
}
