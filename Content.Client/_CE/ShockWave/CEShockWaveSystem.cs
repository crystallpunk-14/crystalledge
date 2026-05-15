using Robust.Client.Graphics;

namespace Content.Client._CE.ShockWave;

public sealed partial class CEShockWaveSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new CEShockWaveOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<CEShockWaveOverlay>();
    }
}
