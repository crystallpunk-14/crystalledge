using Content.Shared._CE.Bonfire;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.GameStates;

namespace Content.Client._CE.Bonfire;

/// <summary>
/// Client-side bonfire system. Reacts to networked state changes by toggling a sprite overlay
/// layer on the bonfire entity whenever the local player's entity enters <see cref="CEBonfireComponent.UsedBy"/>.
/// </summary>
public sealed partial class CEBonfireSystem : CESharedBonfireSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBonfireComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<CEBonfireComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity is not { } localPlayer)
            return;

        var used = ent.Comp.UsedBy.Contains(localPlayer);

        if (!_sprite.LayerMapTryGet(ent.Owner, ent.Comp.UsedOverlayLayer, out var layerIdx, false))
            return;

        _sprite.LayerSetVisible(ent.Owner, layerIdx, !used);
    }
}
