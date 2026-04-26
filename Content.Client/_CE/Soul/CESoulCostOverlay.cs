using System.Numerics;
using Content.Shared._CE.Soul;
using Content.Shared._CE.Soul.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._CE.Soul;

public sealed class CESoulCostOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private const float FullVisibilityRange = 3f;
    private const float HiddenRange = 5f;

    private const float OutlineOffset = 2f;
    private const float IconTextGap = 4f;

    private static readonly Color OutlineColor = Color.Black.WithAlpha(0.85f);
    private static readonly Color EnoughColor = Color.White;
    private static readonly Color NotEnoughColor = Color.FromHex("#dd4444");

    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly CESharedSoulSystem _soul;

    private readonly Font _font;
    private readonly Texture? _soulIcon;

    public CESoulCostOverlay(
        IEntityManager entManager,
        IPlayerManager player,
        IResourceCache cache)
    {
        _entManager = entManager;
        _player = player;
        _transform = entManager.System<SharedTransformSystem>();
        _soul = entManager.System<CESharedSoulSystem>();

        var fontResource = cache.GetResource<FontResource>("/Fonts/_CE/Vollkorn/VollkornSC-Bold.ttf");
        _font = new VectorFont(fontResource, 16);

        if (cache.TryGetResource<RSIResource>(new ResPath("/Textures/_CE/Effects/soul.rsi"), out var rsi)
            && rsi.RSI.TryGetState("effect", out var state))
        {
            _soulIcon = state.Frame0;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        if (_player.LocalEntity is not { } local)
            return;

        if (!_entManager.TryGetComponent(local, out TransformComponent? playerXform))
            return;

        if (playerXform.MapID != args.MapId)
            return;

        var playerPos = _transform.GetWorldPosition(playerXform);
        var playerSouls = _soul.GetSouls(local);

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        handle.SetTransform(Matrix3x2.Identity);

        var query = _entManager.AllEntityQueryEnumerator<CESoulReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var receiver, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var receiverPos = _transform.GetWorldPosition(xform);
            var distance = Vector2.Distance(receiverPos, playerPos);

            if (distance > HiddenRange)
                continue;

            var alpha = distance <= FullVisibilityRange
                ? 1f
                : 1f - (distance - FullVisibilityRange) / (HiddenRange - FullVisibilityRange);

            if (alpha <= 0f)
                continue;

            var screenPos = Vector2.Transform(receiverPos, matrix);

            // Apply the receiver's screen-space offset (in tile units, Y up positive)
            // independently of camera rotation, mirroring how CEDamagePopupOverlay handles
            // its rise direction.
            screenPos.X += receiver.Offset.X * EyeManager.PixelsPerMeter;
            screenPos.Y -= receiver.Offset.Y * EyeManager.PixelsPerMeter;

            var text = $"{playerSouls}/{receiver.Cost}";
            var hasEnough = playerSouls >= receiver.Cost;
            var color = (hasEnough ? EnoughColor : NotEnoughColor).WithAlpha(alpha);
            var outline = OutlineColor.WithAlpha(alpha * OutlineColor.A);

            var textDims = handle.GetDimensions(_font, text, 1f);
            var iconSize = _soulIcon != null
                ? new Vector2(_soulIcon.Width, _soulIcon.Height)
                : Vector2.Zero;

            var totalWidth = iconSize.X + (iconSize.X > 0 ? IconTextGap : 0f) + textDims.X;
            var maxHeight = MathF.Max(iconSize.Y, textDims.Y);

            // Anchor: center horizontally on the receiver, sit above its world position.
            var origin = new Vector2(screenPos.X - totalWidth / 2f, screenPos.Y - maxHeight);

            if (_soulIcon != null)
            {
                var iconPos = new Vector2(origin.X, origin.Y + (maxHeight - iconSize.Y) / 2f);
                var iconRect = UIBox2.FromDimensions(iconPos, iconSize);
                handle.DrawTextureRect(_soulIcon, iconRect, Color.White.WithAlpha(alpha));
            }

            var textPos = new Vector2(
                origin.X + iconSize.X + (iconSize.X > 0 ? IconTextGap : 0f),
                origin.Y + (maxHeight - textDims.Y) / 2f);

            handle.DrawString(_font, textPos + new Vector2(-OutlineOffset, 0), text, 1f, outline);
            handle.DrawString(_font, textPos + new Vector2(OutlineOffset, 0), text, 1f, outline);
            handle.DrawString(_font, textPos + new Vector2(0, -OutlineOffset), text, 1f, outline);
            handle.DrawString(_font, textPos + new Vector2(0, OutlineOffset), text, 1f, outline);
            handle.DrawString(_font, textPos, text, 1f, color);
        }
    }
}
