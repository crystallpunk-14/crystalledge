using Content.Shared._CE.ShockWave;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;

namespace Content.Client._CE.ShockWave;

public sealed partial class CEShockWaveSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private CEShockWaveOverlay _shockWaveOverlay = default!;

    // Guards against AfterAutoHandleStateEvent firing multiple times for the same entity.
    private readonly HashSet<EntityUid> _registered = new();

    public override void Initialize()
    {
        base.Initialize();

        _shockWaveOverlay = new CEShockWaveOverlay();
        _overlay.AddOverlay(_shockWaveOverlay);

        // AfterAutoHandleStateEvent is guaranteed to fire AFTER the component's networked fields
        // (Sharpness, Width, FalloffPower, Duration) are populated from the server state.
        SubscribeLocalEvent<CEShockWaveComponent, AfterAutoHandleStateEvent>(OnShockWaveStateHandled);
        SubscribeLocalEvent<CEShockWaveComponent, ComponentRemove>(OnShockWaveRemoved);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<CEShockWaveOverlay>();
        _registered.Clear();
    }

    private void OnShockWaveStateHandled(Entity<CEShockWaveComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        // Only register the wave the first time we receive the state.
        if (!_registered.Add(ent.Owner))
            return;

        var xform = Transform(ent.Owner);
        if (xform.MapID == Robust.Shared.Map.MapId.Nullspace)
            return;

        _shockWaveOverlay.AddWave(
            _xform.GetWorldPosition(ent.Owner),
            xform.MapID,
            ent.Comp.FalloffPower,
            ent.Comp.Sharpness,
            ent.Comp.Width,
            ent.Comp.Duration
        );
    }

    private void OnShockWaveRemoved(Entity<CEShockWaveComponent> ent, ref ComponentRemove args)
    {
        _registered.Remove(ent.Owner);
    }
}

