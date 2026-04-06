using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using static Robust.Shared.Maths.Color;

namespace Content.Client._CE.Health;

/// <summary>
/// Overlay that draws CE health bars above entities with <see cref="CEMobStateComponent"/> or <see cref="CEGOAPComponent"/>.
/// </summary>
public sealed class CEEntityHealthBarOverlay : Overlay
{
    private static readonly Color HealthColor = Color.FromHex("#db3737");
    private static readonly Color HealthDarken = Color.FromHex("#3a2525");
    private static readonly Color CritColor = Color.FromHex("#a72c95");
    private static readonly Color CritDarken = Color.FromHex("#201d21");

    private readonly IEntityManager _entManager;

    private readonly SharedTransformSystem _transform;
    private readonly CESharedDamageableSystem _damageable;
    private readonly SpriteSystem _spriteSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CEEntityHealthBarOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        _transform = _entManager.System<SharedTransformSystem>();
        _damageable = _entManager.System<CESharedDamageableSystem>();
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

        var query = _entManager.AllEntityQueryEnumerator<CEDamageableComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var spriteComponent, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!_entManager.HasComponent<CEMobStateComponent>(uid) &&
                !_entManager.HasComponent<CEGOAPComponent>(uid))
                continue;

            var info = _damageable.GetHealthInfo(uid);

            if (info.MaxHp <= 0)
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

            var yOffset = bounds.Height * EyeManager.PixelsPerMeter / 2 - 3f + 8f;
            var widthOfMob = bounds.Width * EyeManager.PixelsPerMeter;

            var position = new Vector2(-widthOfMob / EyeManager.PixelsPerMeter / 2,
                yOffset / EyeManager.PixelsPerMeter);

            const float startX = 8f;
            var endX = widthOfMob - 8f;

            var isCrit = info.HasMobState && info.MobState == CEMobState.Critical;

            float ratio;
            Color mainColor;
            Color darkenColor;

            if (isCrit && info.DestroyThreshold is > 0)
            {
                ratio = info.RemainingUntilDeath.HasValue
                    ? Math.Clamp((float) info.RemainingUntilDeath.Value / info.DestroyThreshold.Value, 0f, 1f)
                    : 0f;
                mainColor = CritColor;
                darkenColor = CritDarken;
            }
            else
            {
                ratio = isCrit ? 0f : info.Ratio;
                mainColor = HealthColor;
                darkenColor = HealthDarken;
            }

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
