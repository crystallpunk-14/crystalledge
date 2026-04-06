using Content.Shared._CE.Water;
using Content.Shared.Placeable;
using Robust.Client.GameObjects;

namespace Content.Client._CE.Water;

/// <summary>
/// Lowers DrawDepth of entities with <see cref="CEPlaceableDrawDepthComponent"/> below water.
/// If the entity is placed on an <see cref="ItemPlacerComponent"/> surface,
/// raises its DrawDepth above the surface entity instead.
/// </summary>
public sealed class CEPlaceableDrawDepthSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEPlaceableDrawDepthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEPlaceableDrawDepthComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ItemPlacerComponent, ItemPlacedEvent>(OnItemPlaced);
        SubscribeLocalEvent<ItemPlacerComponent, ItemRemovedEvent>(OnItemRemoved);
    }

    private void OnStartup(EntityUid uid, CEPlaceableDrawDepthComponent comp, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!comp.DepthInitialized)
        {
            comp.OriginalDrawDepth = sprite.DrawDepth;
            comp.DepthInitialized = true;
        }

        _sprite.SetDrawDepth((uid, sprite), comp.LoweredDrawDepth);
    }

    private void OnShutdown(EntityUid uid, CEPlaceableDrawDepthComponent comp, ComponentShutdown args)
    {
        if (!comp.DepthInitialized)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.SetDrawDepth((uid, sprite), comp.OriginalDrawDepth);
    }

    private void OnItemPlaced(EntityUid uid, ItemPlacerComponent placer, ref ItemPlacedEvent args)
    {
        if (!TryComp<CEPlaceableDrawDepthComponent>(args.OtherEntity, out var comp))
            return;

        if (!comp.DepthInitialized)
            return;

        if (!TryComp<SpriteComponent>(args.OtherEntity, out var itemSprite))
            return;

        if (!TryComp<SpriteComponent>(uid, out var surfaceSprite))
            return;

        _sprite.SetDrawDepth((args.OtherEntity, itemSprite), surfaceSprite.DrawDepth + 1);
    }

    private void OnItemRemoved(EntityUid uid, ItemPlacerComponent placer, ref ItemRemovedEvent args)
    {
        if (!TryComp<CEPlaceableDrawDepthComponent>(args.OtherEntity, out var comp))
            return;

        if (!comp.DepthInitialized)
            return;

        if (!TryComp<SpriteComponent>(args.OtherEntity, out var itemSprite))
            return;

        _sprite.SetDrawDepth((args.OtherEntity, itemSprite), comp.LoweredDrawDepth);
    }
}
