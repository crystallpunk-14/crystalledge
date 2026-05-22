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

    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;
    private readonly IGameTiming _timing;
    private readonly Font _font;

    public CEDPSMeterOverlay(IEntityManager entManager, IResourceCache cache, IGameTiming timing)
    {
        _entManager = entManager;
        _transform = entManager.System<SharedTransformSystem>();
        _timing = timing;

        var fontResource = cache.GetResource<FontResource>("/Fonts/_CE/Vollkorn/VollkornSC-Bold.ttf");
        _font = new VectorFont(fontResource, 14);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        handle.SetTransform(Matrix3x2.Identity);

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var scale = new Vector2(matrix.M11, matrix.M12).Length();
        var curTime = _timing.CurTime;

        var query = _entManager.AllEntityQueryEnumerator<CEDPSMeterComponent, TransformComponent>();
        while (query.MoveNext(out _, out var meter, out var xform))
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

            // Live DPS — denominator grows every frame so value decreases naturally.
            var elapsed = (curTime - meter.StartTrackTime).TotalSeconds;
            var liveDPS = (float)(meter.TotalDamage / Math.Max(elapsed, 1.0));

            var totalText = $"Total: {meter.TotalDamage}";
            var maxText   = $"Max: {meter.MaxDPS:F1}";
            var dpsText   = $"DPS: {liveDPS:F1}";
            var timeText  = $"Time: {elapsed:F1}s";

            var worldPos = _transform.GetWorldPosition(xform);
            var screenPos = Vector2.Transform(worldPos, matrix);
            screenPos.X += meter.Offset.X * scale;
            screenPos.Y -= meter.Offset.Y * scale;

            var totalDims = handle.GetDimensions(_font, totalText, 1f);
            var maxDims   = handle.GetDimensions(_font, maxText,   1f);
            var dpsDims   = handle.GetDimensions(_font, dpsText,   1f);
            var timeDims  = handle.GetDimensions(_font, timeText,  1f);

            var blockHeight = totalDims.Y + LineGap + maxDims.Y + LineGap + dpsDims.Y + LineGap + timeDims.Y;

            var totalPos = new Vector2(screenPos.X - totalDims.X / 2f, screenPos.Y - blockHeight);
            var maxPos   = new Vector2(screenPos.X - maxDims.X   / 2f, totalPos.Y + totalDims.Y + LineGap);
            var dpsPos   = new Vector2(screenPos.X - dpsDims.X   / 2f, maxPos.Y   + maxDims.Y   + LineGap);
            var timePos  = new Vector2(screenPos.X - timeDims.X  / 2f, dpsPos.Y   + dpsDims.Y   + LineGap);

            DrawOutlined(handle, totalPos, totalText, MaxColor.WithAlpha(alpha));
            DrawOutlined(handle, maxPos,   maxText,   MaxColor.WithAlpha(alpha));
            DrawOutlined(handle, dpsPos,   dpsText,   DPSColor.WithAlpha(alpha));
            DrawOutlined(handle, timePos,  timeText,  DPSColor.WithAlpha(alpha));
        }
    }

    private void DrawOutlined(DrawingHandleScreen handle, Vector2 pos, string text, Color color)
    {
        var outline = OutlineColor.WithAlpha(OutlineColor.A * color.A);
        const float o = OutlineOffset;
        handle.DrawString(_font, pos + new Vector2(-o, 0), text, 1f, outline);
        handle.DrawString(_font, pos + new Vector2(o, 0), text, 1f, outline);
        handle.DrawString(_font, pos + new Vector2(0, -o), text, 1f, outline);
        handle.DrawString(_font, pos + new Vector2(0, o), text, 1f, outline);
        handle.DrawString(_font, pos, text, 1f, color);
    }
}
