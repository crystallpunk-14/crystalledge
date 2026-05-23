using System.Collections.Generic;
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

    private static readonly Color OutlineColor   = Color.Black.WithAlpha(0.85f);
    private static readonly Color EnoughColor    = Color.White;
    private static readonly Color NotEnoughColor = Color.FromHex("#dd4444");

    // Pre-calculated cardinal-direction offsets — created once, reused every draw call.
    private static readonly Vector2 OLeft  = new(-OutlineOffset, 0f);
    private static readonly Vector2 ORight = new(OutlineOffset,  0f);
    private static readonly Vector2 OUp    = new(0f, -OutlineOffset);
    private static readonly Vector2 ODown  = new(0f,  OutlineOffset);

    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly CESharedSoulSystem _soul;

    private readonly Font _font;
    private readonly Texture? _soulIcon;

    // Icon dimensions and text-start offset within a row — constant after constructor.
    private readonly Vector2 _iconSize;
    private readonly float   _iconTextOffset;

    // Per-entity label cache — avoids GetDimensions + string alloc when playerSouls/cost unchanged.
    private readonly Dictionary<EntityUid, SoulLabelCache> _cache = new();

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

        _iconSize       = _soulIcon != null ? new Vector2(_soulIcon.Width, _soulIcon.Height) : Vector2.Zero;
        _iconTextOffset = _iconSize.X > 0f ? _iconSize.X + IconTextGap : 0f;
    }

    public void ClearCache(EntityUid uid) => _cache.Remove(uid);

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

        var playerPos   = _transform.GetWorldPosition(playerXform);
        var playerSouls = _soul.GetSouls(local);

        var handle  = args.ScreenHandle;
        var matrix  = args.ViewportControl.GetWorldToScreenMatrix();
        // Pixel-per-meter at the current zoom level (accounts for zoom but not rotation).
        var scale   = new Vector2(matrix.M11, matrix.M12).Length();
        handle.SetTransform(Matrix3x2.Identity);

        var vb      = args.ViewportBounds;
        var vLeft   = vb.Left   - 128f;
        var vRight  = vb.Right  + 128f;
        var vTop    = vb.Top    - 64f;
        var vBottom = vb.Bottom + 64f;

        var query = _entManager.AllEntityQueryEnumerator<CESoulReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var receiver, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var receiverPos = _transform.GetWorldPosition(xform);
            var distance    = Vector2.Distance(receiverPos, playerPos);

            if (distance > HiddenRange)
                continue;

            var alpha = distance <= FullVisibilityRange
                ? 1f
                : 1f - (distance - FullVisibilityRange) / (HiddenRange - FullVisibilityRange);

            if (alpha <= 0f)
                continue;

            // Project entity position to screen, then apply offset in screen space
            // (scaled by current zoom) so the label stays above the entity regardless
            // of camera rotation or zoom level.
            var screenPos = Vector2.Transform(receiverPos, matrix);
            screenPos.X  += receiver.Offset.X * scale;
            screenPos.Y  -= receiver.Offset.Y * scale;

            // Broad-phase cull before text metrics.
            if (screenPos.X < vLeft || screenPos.X > vRight ||
                screenPos.Y < vTop  || screenPos.Y > vBottom)
                continue;

            // Retrieve or create cache entry; refresh only when playerSouls or cost changed.
            if (!_cache.TryGetValue(uid, out var c))
            {
                c = new SoulLabelCache();
                _cache[uid] = c;
            }

            if (c.LastSouls != playerSouls || c.LastCost != receiver.Cost)
            {
                c.LastSouls  = playerSouls;
                c.LastCost   = receiver.Cost;
                c.Text       = $"{playerSouls}/{receiver.Cost}";
                c.TextDims   = handle.GetDimensions(_font, c.Text, 1f);
                c.MaxHeight  = MathF.Max(_iconSize.Y, c.TextDims.Y);
                c.TotalWidth = _iconTextOffset + c.TextDims.X;
                c.TextLocalY = (c.MaxHeight - c.TextDims.Y) / 2f;
                c.IconLocalY = (c.MaxHeight - _iconSize.Y)  / 2f;
            }

            var hasEnough = playerSouls >= receiver.Cost;
            var color     = (hasEnough ? EnoughColor : NotEnoughColor).WithAlpha(alpha);
            var outline   = OutlineColor.WithAlpha(alpha * OutlineColor.A);

            // Anchor: center horizontally on the receiver, sit above its world position.
            var origin = new Vector2(screenPos.X - c.TotalWidth / 2f, screenPos.Y - c.MaxHeight);

            if (_soulIcon != null)
            {
                var iconPos  = new Vector2(origin.X, origin.Y + c.IconLocalY);
                var iconRect = UIBox2.FromDimensions(iconPos, _iconSize);
                handle.DrawTextureRect(_soulIcon, iconRect, Color.White.WithAlpha(alpha));
            }

            var textPos = new Vector2(origin.X + _iconTextOffset, origin.Y + c.TextLocalY);
            handle.DrawString(_font, textPos + OLeft,  c.Text, 1f, outline);
            handle.DrawString(_font, textPos + ORight, c.Text, 1f, outline);
            handle.DrawString(_font, textPos + OUp,    c.Text, 1f, outline);
            handle.DrawString(_font, textPos + ODown,  c.Text, 1f, outline);
            handle.DrawString(_font, textPos,          c.Text, 1f, color);
        }
    }

    private sealed class SoulLabelCache
    {
        public int     LastSouls  = int.MinValue;
        public int     LastCost   = int.MinValue;
        public string  Text       = string.Empty;
        public Vector2 TextDims;
        public float   MaxHeight;
        public float   TotalWidth;
        public float   TextLocalY;
        public float   IconLocalY;
    }
}
