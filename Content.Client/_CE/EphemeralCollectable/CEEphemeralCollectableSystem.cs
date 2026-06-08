using Content.Shared._CE.EphemeralCollectable;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CE.EphemeralCollectable;

/// <summary>
/// Client-side visuals for <see cref="CEEphemeralCollectableComponent"/>.
/// Hides the sprite and disables any point light locally if the local player
/// is in <see cref="CEEphemeralCollectableComponent.CollectedBy"/>.
/// The entity is not removed from the world — only hidden on this client.
/// </summary>
public sealed partial class CEEphemeralCollectableSystem : CESharedEphemeralCollectableSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private PointLightSystem _light = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEphemeralCollectableComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<CEEphemeralCollectableComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEEphemeralCollectableComponent, CEEphemeralCollectedEvent>(OnCollected);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
    }

    private void OnCollected(Entity<CEEphemeralCollectableComponent> ent, ref CEEphemeralCollectedEvent args)
    {
        // Predicted collection just happened locally — refresh visuals immediately
        // so the local player sees the entity hide without waiting for server state.
        UpdateVisuals(ent);

        // Spawn the configured client-side VFX only if the local player is the collector.
        // This is purely cosmetic — server-spawned VFX would be delayed, so we do it locally
        // for instant feedback on collection.
        if (ent.Comp.CollectVfx is not { } vfxProto)
            return;

        if (_player.LocalEntity != args.Player)
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        Spawn(vfxProto, Transform(ent).Coordinates);
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        if (ev.Entity != _player.LocalEntity)
            return;

        var query = EntityQueryEnumerator<CEEphemeralCollectableComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateVisuals((uid, comp));
        }
    }

    private void OnStartup(Entity<CEEphemeralCollectableComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    private void OnAfterAutoHandleState(Entity<CEEphemeralCollectableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<CEEphemeralCollectableComponent> ent)
    {
        var localPlayer = _player.LocalEntity;
        var collected = localPlayer is not null && ent.Comp.CollectedBy.Contains(localPlayer.Value);

        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetVisible((ent, sprite), !collected);

        if (TryComp<PointLightComponent>(ent, out var light))
            _light.SetEnabled(ent, !collected, light);
    }
}
