using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Stamina;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using static Robust.Shared.Maths.Color;

namespace Content.Client._CE.Stamina;

/// <summary>
/// Overlay that draws CE stamina bars above entities with <see cref="CEStaminaComponent"/>.
/// Drawn below health and mana bars.
/// </summary>
public sealed class CEEntityStaminaBarOverlay : Overlay
{
    private static readonly Color StaminaColor = Color.FromHex("#44e32b");
    private static readonly Color StaminaDarken = Color.FromHex("#334033");
    private static readonly Color ExhaustedColor = Color.FromHex("#f2a93a");
    private static readonly Color ExhaustedDarken = Color.FromHex("#3a3328");

    private readonly IEntityManager _entManager;

    private readonly SharedTransformSystem _transform;
    private readonly CEStaminaSystem _stamina;
    private readonly SpriteSystem _spriteSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CEEntityStaminaBarOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        _transform = _entManager.System<SharedTransformSystem>();
        _stamina = _entManager.System<CEStaminaSystem>();
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

        var query = _entManager.AllEntityQueryEnumerator<CEStaminaComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var stamina, out var spriteComponent, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!_entManager.HasComponent<CEMobStateComponent>(uid) &&
                !_entManager.HasComponent<CEGOAPComponent>(uid))
                continue;

            if (stamina.MaxStamina <= 0)
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

            var yOffset = bounds.Height * EyeManager.PixelsPerMeter / 2 - 3f;
            var widthOfMob = bounds.Width * EyeManager.PixelsPerMeter;

            var position = new Vector2(-widthOfMob / EyeManager.PixelsPerMeter / 2,
                yOffset / EyeManager.PixelsPerMeter);

            const float startX = 8f;
            var endX = widthOfMob - 8f;

            var current = _stamina.GetStamina((uid, stamina));
            var ratio = Math.Clamp(current / stamina.MaxStamina, 0f, 1f);
            var xProgress = (endX - startX) * ratio + startX;

            var mainColor = stamina.Exhausted ? ExhaustedColor : StaminaColor;
            var darkenColor = stamina.Exhausted ? ExhaustedDarken : StaminaDarken;

            var boxBackground = new Box2(
                new Vector2(startX - 0.5f, -0.5f) / EyeManager.PixelsPerMeter,
                new Vector2(endX + 0.5f, 3.5f) / EyeManager.PixelsPerMeter);
            boxBackground = boxBackground.Translated(position);
            handle.DrawRect(boxBackground, Black.WithAlpha(192));

            var boxMain = new Box2(
                new Vector2(startX, 0f) / EyeManager.PixelsPerMeter,
                new Vector2(xProgress, 3f) / EyeManager.PixelsPerMeter);
            boxMain = boxMain.Translated(position);
            handle.DrawRect(boxMain, mainColor);

            var pixelDarken = new Box2(
                new Vector2(startX, 2f) / EyeManager.PixelsPerMeter,
                new Vector2(xProgress, 3f) / EyeManager.PixelsPerMeter);
            pixelDarken = pixelDarken.Translated(position);
            handle.DrawRect(pixelDarken, darkenColor);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
