using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using static Robust.Shared.Maths.Color;

namespace Content.Client._CE.Mana;

/// <summary>
/// Overlay that draws CE mana bars above entities with <see cref="CEMagicEnergyContainerComponent"/>.
/// Drawn below the health bar.
/// </summary>
public sealed class CEEntityManaBarOverlay : Overlay
{
    private static readonly Color ManaColor = Color.FromHex("#3fb5c4");
    private static readonly Color ManaDarken = Color.FromHex("#2a2847");

    private readonly IEntityManager _entManager;

    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _spriteSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CEEntityManaBarOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        _transform = _entManager.System<SharedTransformSystem>();
        _spriteSystem = _entManager.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        const float scale = 1f;
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(scale, scale));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);

        var query = _entManager.AllEntityQueryEnumerator<CEMagicEnergyContainerComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mana, out var spriteComponent, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!_entManager.HasComponent<CEMobStateComponent>(uid) &&
                !_entManager.HasComponent<CEGOAPComponent>(uid))
                continue;

            if (mana.MaxEnergy <= 0)
                continue;

            var bounds = _entManager.GetComponentOrNull<StatusIconComponent>(uid)?.Bounds
                         ?? _spriteSystem.GetLocalBounds((uid, spriteComponent));

            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);

            handle.SetTransform(matty);

            var yOffset = bounds.Height * EyeManager.PixelsPerMeter / 2 - 3f + 4f;
            var widthOfMob = bounds.Width * EyeManager.PixelsPerMeter;

            var position = new Vector2(-widthOfMob / EyeManager.PixelsPerMeter / 2,
                yOffset / EyeManager.PixelsPerMeter);

            const float startX = 8f;
            var endX = widthOfMob - 8f;

            var ratio = Math.Clamp((float) mana.Energy / mana.MaxEnergy, 0f, 1f);
            var xProgress = (endX - startX) * ratio + startX;

            var boxBackground = new Box2(
                new Vector2(startX - 0.5f, -0.5f) / EyeManager.PixelsPerMeter,
                new Vector2(endX + 0.5f, 3.5f) / EyeManager.PixelsPerMeter);
            boxBackground = boxBackground.Translated(position);
            handle.DrawRect(boxBackground, Black.WithAlpha(192));

            var boxMain = new Box2(
                new Vector2(startX, 0f) / EyeManager.PixelsPerMeter,
                new Vector2(xProgress, 3f) / EyeManager.PixelsPerMeter);
            boxMain = boxMain.Translated(position);
            handle.DrawRect(boxMain, ManaColor);

            var pixelDarken = new Box2(
                new Vector2(startX, 2f) / EyeManager.PixelsPerMeter,
                new Vector2(xProgress, 3f) / EyeManager.PixelsPerMeter);
            pixelDarken = pixelDarken.Translated(position);
            handle.DrawRect(pixelDarken, ManaDarken);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
