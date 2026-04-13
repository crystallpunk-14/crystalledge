using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client._CE.Health;

/// <summary>
/// Renders floating damage/heal numbers in screen space (world positions converted to screen).
/// Numbers float upward, then pause, shrink and fade out.
/// </summary>
public sealed class CEDamagePopupOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    /// <summary>
    /// Initial screen-space Y offset in pixels to spawn the popup above the entity center.
    /// Applied in screen space so camera rotation doesn't affect direction.
    /// </summary>
    private const float InitialScreenYOffset = 32f;

    private readonly Font _fontSmall;
    private readonly Font _fontMedium;
    private readonly Font _fontLarge;

    /// <summary>
    /// Outline offset in pixels. Each text is drawn 4 times at this offset in cardinal directions.
    /// </summary>
    private const float OutlineOffset = 3f;

    private static readonly Color OutlineColor = Color.Black.WithAlpha(0.85f);
    public readonly List<PopupEntry> Entries = new();

    public CEDamagePopupOverlay(IResourceCache cache)
    {
        var fontResource = cache.GetResource<FontResource>("/Fonts/_CE/Vollkorn/VollkornSC-Bold.ttf");
        _fontSmall = new VectorFont(fontResource, 40);
        _fontMedium = new VectorFont(fontResource, 60);
        _fontLarge = new VectorFont(fontResource, 80);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();

        handle.SetTransform(Matrix3x2.Identity);

        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            var entry = Entries[i];

            var progress = (float) entry.Elapsed / entry.Duration;

            // Phase timings (as fraction of total duration):
            // 0.0 – 0.15 : fade-in + rise
            // 0.15 – 0.60 : rise continues (decelerating)
            // 0.60 – 0.75 : hang
            // 0.75 – 1.0  : shrink + fade out

            float alpha;
            float scale;
            float yPixelOffset;

            // RiseHeight is in world units; convert to pixels using PixelsPerMeter.
            var risePixels = entry.RiseHeight * EyeManager.PixelsPerMeter;

            if (progress < 0.15f)
            {
                var t = progress / 0.15f;
                alpha = t;
                scale = 1f;
                yPixelOffset = EaseOutQuad(progress / 0.6f) * risePixels;
            }
            else if (progress < 0.6f)
            {
                alpha = 1f;
                scale = 1f;
                yPixelOffset = EaseOutQuad(progress / 0.6f) * risePixels;
            }
            else if (progress < 0.75f)
            {
                alpha = 1f;
                scale = 1f;
                yPixelOffset = risePixels;
            }
            else
            {
                var t = (progress - 0.75f) / 0.25f;
                alpha = 1f - t;
                scale = 1f - t * 0.6f;
                yPixelOffset = risePixels;
            }

            // Gentle alpha oscillation between 0.9 and 1.0 during visible phase.
            if (alpha > 0f)
            {
                var flicker = 0.95f + 0.05f * MathF.Sin(progress * MathF.PI * 8f);
                alpha *= flicker;
            }

            if (alpha <= 0f)
                continue;

            var font = entry.FontSize switch
            {
                PopupFontSize.Small => _fontSmall,
                PopupFontSize.Medium => _fontMedium,
                _ => _fontLarge,
            };

            // Convert spawn world position to screen, then offset in screen space.
            // All offsets are in pixels so they work correctly regardless of camera rotation.
            // Initial offset: start above the entity center (like vanilla PopupOverlay).
            var screenPos = Vector2.Transform(entry.WorldPosition, matrix);
            screenPos.Y -= yPixelOffset + InitialScreenYOffset;
            screenPos.X += entry.ScreenXOffset;

            var text = entry.Text;
            var dimensions = handle.GetDimensions(font, text, scale);

            // Center text horizontally, anchor at bottom vertically.
            var drawPos = screenPos - new Vector2(dimensions.X / 2f, dimensions.Y);

            var color = entry.Color.WithAlpha(alpha);
            var outline = OutlineColor.WithAlpha(alpha * OutlineColor.A);

            // Draw dark outline (4 cardinal offsets) for readability over sprites.
            handle.DrawString(font, drawPos + new Vector2(-OutlineOffset, 0), text, scale, outline);
            handle.DrawString(font, drawPos + new Vector2(OutlineOffset, 0), text, scale, outline);
            handle.DrawString(font, drawPos + new Vector2(0, -OutlineOffset), text, scale, outline);
            handle.DrawString(font, drawPos + new Vector2(0, OutlineOffset), text, scale, outline);

            // Main colored text on top.
            handle.DrawString(font, drawPos, text, scale, color);
        }
    }

    private static float EaseOutQuad(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - (1f - t) * (1f - t);
    }

    public sealed class PopupEntry
    {
        public Vector2 WorldPosition;
        public string Text = string.Empty;
        public Color Color;
        public PopupFontSize FontSize;
        public float Duration;
        public float RiseHeight;
        public float ScreenXOffset;
        public double Elapsed;
    }

    public enum PopupFontSize : byte
    {
        Small,
        Medium,
        Large,
    }
}
