using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._CE.ScreenPopup;

/// <summary>
/// Full-screen cinematic popup control that fades in a title and description, holds for a moment, then signals
/// the animation is complete. Queued and driven by <see cref="CEClientScreenPopupSystem"/>.
/// </summary>
public sealed class CEScreenPopupControl : Control
{
    private const float FadeDuration = 4f;
    private const float HoldTime = 3f;
    private const float FadeOutDuration = 2f;

    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly FontTagHijackHolder _fontHijack = default!;

    public event Action? OnAnimationEnd;

    private readonly RichTextLabel _titleLabel;
    private readonly RichTextLabel _reasonLabel;

    private float _elapsedTime;
    private float _holdElapsedTime;
    private float _fadeOutElapsedTime;
    private bool _completed;

    public CEScreenPopupControl()
    {
        IoCManager.InjectDependencies(this);

        _titleLabel = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(10),
        };

        _reasonLabel = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(10),
        };

        var vbox = new BoxContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };

        vbox.AddChild(_titleLabel);
        vbox.AddChild(_reasonLabel);
        AddChild(vbox);

        vbox.Margin = new Thickness(0, 120, 0, 0);

        // Register custom font proto aliases for markup tags:
        //   [font="_ce_popup_title"] → VollkornSC-Bold
        //   [font="_ce_popup_reason"] → VollkornSC-Regular
        var previousHijack = _fontHijack.Hijack;
        _fontHijack.Hijack = (protoId, size) =>
        {
            if (protoId == "_ce_popup_title")
                return _resourceCache.GetFont("/Fonts/_CE/Vollkorn/VollkornSC-Bold.ttf", size);

            if (protoId == "_ce_popup_reason")
                return _resourceCache.GetFont("/Fonts/_CE/Vollkorn/VollkornSC-Regular.ttf", size);

            return previousHijack?.Invoke(protoId, size);
        };

        _fontHijack.HijackUpdated();
    }

    /// <summary>
    /// Starts displaying the popup contents. Resets the animation timer.
    /// </summary>
    public void AnimationStart(string title, string description)
    {
        var titleMarkup = $"[font=\"_ce_popup_title\" size=64]{title}[/font]";
        var reasonMarkup = $"[font=\"_ce_popup_reason\" size=36]{description}[/font]";

        _titleLabel.SetMessage(FormattedMessage.FromMarkupPermissive(titleMarkup), tagsAllowed: null, Color.White);
        _reasonLabel.SetMessage(FormattedMessage.FromMarkupPermissive(reasonMarkup), tagsAllowed: null, Color.White);

        _elapsedTime = 0f;
        _holdElapsedTime = 0f;
        _fadeOutElapsedTime = 0f;
        _completed = false;

        Modulate = Color.White.WithAlpha(0f);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_completed)
            return;

        // Phase 1: fade in.
        if (_elapsedTime < FadeDuration)
        {
            _elapsedTime += args.DeltaSeconds;
            var alpha = MathHelper.Lerp(0f, 1f, _elapsedTime / FadeDuration);
            Modulate = Color.White.WithAlpha(alpha);
            return;
        }

        // Phase 2: hold at full opacity.
        if (_holdElapsedTime < HoldTime)
        {
            _holdElapsedTime += args.DeltaSeconds;
            Modulate = Color.White;
            return;
        }

        // Phase 3: fade out.
        _fadeOutElapsedTime += args.DeltaSeconds;
        var fadeAlpha = MathHelper.Lerp(1f, 0f, _fadeOutElapsedTime / FadeOutDuration);
        Modulate = Color.White.WithAlpha(Math.Max(fadeAlpha, 0f));

        if (_fadeOutElapsedTime >= FadeOutDuration)
        {
            _completed = true;
            OnAnimationEnd?.Invoke();
        }
    }
}
