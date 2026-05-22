using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CE.DPSMeter;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._CE.DPSMeter;

/// <summary>
/// Draws a two-line overlay above entities that carry <see cref="CEDPSMeterComponent"/>:
/// <list type="bullet">
///   <item>Top: "Max: X.X" — peak DPS reached this session (static)</item>
///   <item>Bottom: "DPS: X.X" — live value, decreases as time passes between hits</item>
/// </list>
/// The overlay appears instantly on first hit and fades out after <see cref="CEDPSMeterComponent.TrackTimeAfterHit"/>
/// seconds of silence over a <see cref="CEDPSMeterComponent.FadeDuration"/> window.
/// Uses the same Vollkorn font as <c>CESoulCostOverlay</c>.
/// </summary>
public sealed class CEDPSMeterOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private static readonly Color MaxColor = Color.White;
    private static readonly Color DPSColor = Color.Yellow;
    private static readonly Color OutlineColor = Color.Black.WithAlpha(0.85f);

    private const float OutlineOffset = 2f;
    private const float LineGap = 2f;

    // Pre-calculated cardinal-direction offsets — created once, reused every draw call.
    private static readonly Vector2 OLeft  = new(-OutlineOffset, 0f);
    private static readonly Vector2 ORight = new(OutlineOffset, 0f);
    private static readonly Vector2 OUp = new(0f, -OutlineOffset);
    private static readonly Vector2 ODown  = new(0f,  OutlineOffset);

    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;
    private readonly IGameTiming _timing;
    private readonly Font _font;

    // Per-entity text + dimension cache — only recomputed when underlying value changes.
    private readonly Dictionary<EntityUid, DPSCache> _cache = new();

    public CEDPSMeterOverlay(IEntityManager entManager, IResourceCache cache, IGameTiming timing)
    {
        _entManager = entManager;
        _transform = entManager.System<SharedTransformSystem>();
        _timing = timing;

        var fontResource = cache.GetResource<FontResource>("/Fonts/_CE/Vollkorn/VollkornSC-Bold.ttf");
        _font = new VectorFont(fontResource, 14);
    }

    public void ClearCache(EntityUid uid) => _cache.Remove(uid);

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        handle.SetTransform(Matrix3x2.Identity);

        var matrix  = args.ViewportControl.GetWorldToScreenMatrix();
        var scale   = new Vector2(matrix.M11, matrix.M12).Length();
        var curTime = _timing.CurTime;

        var vb= args.ViewportBounds;
        var vLeft = vb.Left - 128f;
        var vRight= vb.Right + 128f;
        var vTop= vb.Top - 64f;
        var vBottom = vb.Bottom + 64f;

        var query = _entManager.AllEntityQueryEnumerator<CEDPSMeterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meter, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (meter.TotalDamage <= 0 || meter.StartTrackTime == TimeSpan.Zero)
                continue;

            // Fade-out: full opacity while within TrackTimeAfterHit, then lerp to 0 over FadeDuration.
            var timeSinceHit = (curTime - meter.LastHitTime).TotalSeconds;
            var fadeStart = meter.TrackTimeAfterHit.TotalSeconds;
            var fadeEnd = fadeStart + meter.FadeDuration.TotalSeconds;

            float alpha;
            if (timeSinceHit < fadeStart)
                alpha = 1f;
            else if (timeSinceHit < fadeEnd)
                alpha = 1f - (float)((timeSinceHit - fadeStart) / (fadeEnd - fadeStart));
            else
                alpha = 0f;

            if (alpha <= 0f)
                continue;

            var worldPos  = _transform.GetWorldPosition(xform);
            var screenPos = Vector2.Transform(worldPos, matrix);
            screenPos.X += meter.Offset.X * scale;
            screenPos.Y -= meter.Offset.Y * scale;

            // Broad-phase cull before string formatting and text metrics.
            if (screenPos.X < vLeft || screenPos.X > vRight ||
                screenPos.Y < vTop  || screenPos.Y > vBottom)
                continue;

            // Live DPS — denominator grows every frame so value decreases naturally.
            var elapsed= (curTime - meter.StartTrackTime).TotalSeconds;
            var liveDPS = (float)(meter.TotalDamage / Math.Max(elapsed, 1.0));
            var dpsRounded = MathF.Round(liveDPS, 1);
            var timeRounded = MathF.Round((float)elapsed, 1);

            // Retrieve or create per-entity cache; refresh each line only when its value changed.
            if (!_cache.TryGetValue(uid, out var c))
            {
                c = new DPSCache();
                _cache[uid] = c;
            }

            if (c.LastTotal != meter.TotalDamage)
            {
                c.LastTotal = meter.TotalDamage;
                c.TotalText = $"Total: {meter.TotalDamage}";
                c.TotalDims = handle.GetDimensions(_font, c.TotalText, 1f);
            }

            if (Math.Abs(c.LastMaxDPS - meter.MaxDPS) > 0.01f)
            {
                c.LastMaxDPS = meter.MaxDPS;
                c.MaxText    = $"Max: {meter.MaxDPS:F1}";
                c.MaxDims    = handle.GetDimensions(_font, c.MaxText, 1f);
            }

            if (Math.Abs(c.LastDPSRounded - dpsRounded) > 0.01f)
            {
                c.LastDPSRounded = dpsRounded;
                c.DPSText        = $"DPS: {dpsRounded:F1}";
                c.DPSDims        = handle.GetDimensions(_font, c.DPSText, 1f);
            }

            if (Math.Abs(c.LastTimeRounded - timeRounded) > 0.01f)
            {
                c.LastTimeRounded = timeRounded;
                c.TimeText        = $"Time: {timeRounded:F1}s";
                c.TimeDims        = handle.GetDimensions(_font, c.TimeText, 1f);
            }

            var blockHeight = c.TotalDims.Y + LineGap + c.MaxDims.Y + LineGap + c.DPSDims.Y + LineGap + c.TimeDims.Y;

            var totalPos = new Vector2(screenPos.X - c.TotalDims.X / 2f, screenPos.Y - blockHeight);
            var maxPos   = new Vector2(screenPos.X - c.MaxDims.X   / 2f, totalPos.Y + c.TotalDims.Y + LineGap);
            var dpsPos   = new Vector2(screenPos.X - c.DPSDims.X   / 2f, maxPos.Y   + c.MaxDims.Y   + LineGap);
            var timePos  = new Vector2(screenPos.X - c.TimeDims.X  / 2f, dpsPos.Y   + c.DPSDims.Y   + LineGap);

            DrawOutlined(handle, totalPos, c.TotalText, MaxColor.WithAlpha(alpha));
            DrawOutlined(handle, maxPos,   c.MaxText,   MaxColor.WithAlpha(alpha));
            DrawOutlined(handle, dpsPos,   c.DPSText,   DPSColor.WithAlpha(alpha));
            DrawOutlined(handle, timePos,  c.TimeText,  DPSColor.WithAlpha(alpha));
        }
    }

    private void DrawOutlined(DrawingHandleScreen handle, Vector2 pos, string text, Color color)
    {
        var outline = OutlineColor.WithAlpha(OutlineColor.A * color.A);
        handle.DrawString(_font, pos + OLeft,  text, 1f, outline);
        handle.DrawString(_font, pos + ORight, text, 1f, outline);
        handle.DrawString(_font, pos + OUp,    text, 1f, outline);
        handle.DrawString(_font, pos + ODown,  text, 1f, outline);
        handle.DrawString(_font, pos,          text, 1f, color);
    }

    private sealed class DPSCache
    {
        public int LastTotal = int.MinValue;
        public float LastMaxDPS = float.MinValue;
        public float LastDPSRounded = float.MinValue;
        public float LastTimeRounded = float.MinValue;

        public string TotalText = string.Empty;
        public string MaxText = string.Empty;
        public string DPSText = string.Empty;
        public string TimeText = string.Empty;

        public Vector2 TotalDims;
        public Vector2 MaxDims;
        public Vector2 DPSDims;
        public Vector2 TimeDims;
    }
}
