using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CE.Currency;
using Content.Shared._CE.Trading.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._CE.Trading;

public sealed class CETradingCostOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private const float FullVisibilityRange = 1.5f;
    private const float HiddenRange = 2.5f;
    private const float OutlineOffset = 2f;
    private const float IconSize = 21f;
    private const float IconTextGap = 3f;
    private const float GroupGap = 6f;

    private static readonly Color OutlineColor   = Color.Black.WithAlpha(0.85f);
    private static readonly Color EnoughColor    = Color.White;
    private static readonly Color NotEnoughColor = Color.FromHex("#dd4444");

    private static readonly Vector2 OLeft  = new(-OutlineOffset, 0f);
    private static readonly Vector2 ORight = new(OutlineOffset,  0f);
    private static readonly Vector2 OUp    = new(0f, -OutlineOffset);
    private static readonly Vector2 ODown  = new(0f,  OutlineOffset);

    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly CECurrencySystem _currency;

    private readonly Font _font;
    private readonly Texture? _ppIcon;
    private readonly Texture? _gpIcon;
    private readonly Texture? _spIcon;
    private readonly Texture? _cpIcon;

    private readonly Dictionary<EntityUid, TradingLabelCache> _cache = new();

    public CETradingCostOverlay(
        IEntityManager entManager,
        IPlayerManager player,
        IResourceCache cache)
    {
        _entManager = entManager;
        _player = player;
        _transform = entManager.System<SharedTransformSystem>();
        _currency = entManager.System<CECurrencySystem>();

        var fontResource = cache.GetResource<FontResource>("/Fonts/_CE/Vollkorn/VollkornSC-Bold.ttf");
        _font = new VectorFont(fontResource, 21);

        if (cache.TryGetResource<RSIResource>(new ResPath("/Textures/_CE/Interface/coins.rsi"), out var rsi))
        {
            _ppIcon = rsi.RSI.TryGetState("p", out var pp) ? pp.Frame0 : null;
            _gpIcon = rsi.RSI.TryGetState("g", out var gp) ? gp.Frame0 : null;
            _spIcon = rsi.RSI.TryGetState("s", out var sp) ? sp.Frame0 : null;
            _cpIcon = rsi.RSI.TryGetState("c", out var cp) ? cp.Frame0 : null;
        }
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
        var playerMoney = _currency.GetPriceTotal(local);

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var scale  = new Vector2(matrix.M11, matrix.M12).Length();
        handle.SetTransform(Matrix3x2.Identity);

        var vb     = args.ViewportBounds;
        var vLeft  = vb.Left   - 200f;
        var vRight = vb.Right  + 200f;
        var vTop   = vb.Top    - 64f;
        var vBottom = vb.Bottom + 64f;

        var query = _entManager.AllEntityQueryEnumerator<CETradingSlotComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var slot, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (slot.ActivePreviewProto == null)
                continue;

            var slotPos  = _transform.GetWorldPosition(xform);
            var distance = Vector2.Distance(slotPos, playerPos);

            if (distance > HiddenRange)
                continue;

            var alpha = distance <= FullVisibilityRange
                ? 1f
                : 1f - (distance - FullVisibilityRange) / (HiddenRange - FullVisibilityRange);

            if (alpha <= 0f)
                continue;

            var screenPos = Vector2.Transform(slotPos, matrix);
            screenPos.X  += slot.Offset.X * scale;
            screenPos.Y  -= slot.Offset.Y * scale;

            if (screenPos.X < vLeft || screenPos.X > vRight ||
                screenPos.Y < vTop  || screenPos.Y > vBottom)
                continue;

            if (!_cache.TryGetValue(uid, out var c))
            {
                c = new TradingLabelCache();
                _cache[uid] = c;
            }

            if (c.LastPlayerMoney != playerMoney || c.LastPrice != slot.Price)
            {
                c.LastPlayerMoney = playerMoney;
                c.LastPrice       = slot.Price;
                BuildCache(c, slot.Price, handle);
            }

            if (c.Entries.Count == 0)
                continue;

            var hasEnough = playerMoney >= slot.Price;
            var textColor = (hasEnough ? EnoughColor : NotEnoughColor).WithAlpha(alpha);
            var outline   = OutlineColor.WithAlpha(alpha * OutlineColor.A);

            var origin = new Vector2(screenPos.X - c.TotalWidth / 2f, screenPos.Y - c.MaxHeight);
            var x      = origin.X;

            foreach (var entry in c.Entries)
            {
                if (entry.Icon != null)
                {
                    var iconPos  = new Vector2(x, origin.Y + (c.MaxHeight - IconSize) / 2f);
                    var iconRect = UIBox2.FromDimensions(iconPos, new Vector2(IconSize, IconSize));
                    handle.DrawTextureRect(entry.Icon, iconRect, Color.White.WithAlpha(alpha));
                }

                var textX   = x + IconSize + IconTextGap;
                var textPos = new Vector2(textX, origin.Y + (c.MaxHeight - entry.TextDims.Y) / 2f);

                handle.DrawString(_font, textPos + OLeft,  entry.Text, 1f, outline);
                handle.DrawString(_font, textPos + ORight, entry.Text, 1f, outline);
                handle.DrawString(_font, textPos + OUp,    entry.Text, 1f, outline);
                handle.DrawString(_font, textPos + ODown,  entry.Text, 1f, outline);
                handle.DrawString(_font, textPos,          entry.Text, 1f, textColor);

                x += entry.Width + GroupGap;
            }
        }
    }

    private void BuildCache(TradingLabelCache c, int price, DrawingHandleScreen handle)
    {
        c.Entries.Clear();

        var remaining = price;
        TryAddDenom(c, _ppIcon, remaining / 1000, handle);
        remaining %= 1000;
        TryAddDenom(c, _gpIcon, remaining / 100, handle);
        remaining %= 100;
        TryAddDenom(c, _spIcon, remaining / 10, handle);
        remaining %= 10;
        TryAddDenom(c, _cpIcon, remaining, handle);

        // Compute total width and max height
        c.TotalWidth = 0f;
        c.MaxHeight  = IconSize;
        foreach (var entry in c.Entries)
        {
            c.TotalWidth += entry.Width;
            c.MaxHeight   = MathF.Max(c.MaxHeight, entry.TextDims.Y);
        }
        if (c.Entries.Count > 1)
            c.TotalWidth += GroupGap * (c.Entries.Count - 1);
    }

    private void TryAddDenom(TradingLabelCache c, Texture? icon, int amount, DrawingHandleScreen handle)
    {
        if (amount <= 0)
            return;

        var text     = amount.ToString();
        var textDims = handle.GetDimensions(_font, text, 1f);
        var width    = IconSize + IconTextGap + textDims.X;

        c.Entries.Add(new DenomEntry
        {
            Icon     = icon,
            Text     = text,
            TextDims = textDims,
            Width    = width,
        });
    }

    private sealed class DenomEntry
    {
        public Texture? Icon;
        public string   Text    = string.Empty;
        public Vector2  TextDims;
        public float    Width;
    }

    private sealed class TradingLabelCache
    {
        public int  LastPlayerMoney = int.MinValue;
        public int  LastPrice       = int.MinValue;
        public readonly List<DenomEntry> Entries = new();
        public float TotalWidth;
        public float MaxHeight;
    }
}
