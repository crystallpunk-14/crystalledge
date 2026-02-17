using System.Numerics;
using Content.Shared._CE.Achievements.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._CE.Achievements;

/// <summary>
/// Achievement notification popup that slides in from bottom right, displays for 2 seconds, then slides out.
/// Similar to Steam achievement notifications.
/// </summary>
public sealed class CEAchievementNotificationControl : Control
{
    private const float SlideDuration = 0.5f; // Time to slide in/out
    private const float DisplayTime = 2.0f;   // Time to stay visible

    [Dependency] private readonly IEntityManager _entManager = default!;
    private readonly SpriteSystem _spriteSystem = default!;

    public event Action? OnAnimationEnd;

    private readonly PanelContainer _panel;
    private readonly BoxContainer _hbox;
    private readonly TextureRect _iconRect;
    private readonly BoxContainer _textVbox;
    private readonly Label _titleLabel;
    private readonly RichTextLabel _descriptionLabel;
    private readonly Label _percentageLabel;

    private float _elapsedTime;
    private AnimationState _animationState;
    private float _offscreenMarginRight;
    private float _onscreenMarginRight;

    private enum AnimationState
    {
        SlideIn,
        Display,
        SlideOut
    }

    public CEAchievementNotificationControl()
    {
        IoCManager.InjectDependencies(this);

        _spriteSystem = _entManager.System<SpriteSystem>();

        // Panel with background
        _panel = new PanelContainer
        {
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            MinWidth = 300,
            MaxWidth = 400
        };
        _panel.AddStyleClass("AchievementNotificationPanel");

        // Horizontal layout
        _hbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        // Achievement icon
        _iconRect = new TextureRect
        {
            MinWidth = 48,
            MinHeight = 48,
            MaxWidth = 48,
            MaxHeight = 48,
            Stretch = TextureRect.StretchMode.Scale,
            VerticalAlignment = VAlignment.Center
        };

        // Text container
        _textVbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10, 0, 0, 0)
        };

        // "Achievement Unlocked" title
        _titleLabel = new Label
        {
            Text = Loc.GetString("ce-achievement-unlocked"),
            FontColorOverride = Color.Gold,
            Margin = new Thickness(0, 0, 0, 2)
        };
        _titleLabel.AddStyleClass("LabelHeading");

        // Achievement name and description
        _descriptionLabel = new RichTextLabel
        {
            HorizontalExpand = true
        };

        // Percentage label
        _percentageLabel = new Label
        {
            HorizontalAlignment = HAlignment.Right,
            FontColorOverride = Color.LightGray,
            Margin = new Thickness(0, 2, 0, 0)
        };

        // Build hierarchy
        _textVbox.AddChild(_titleLabel);
        _textVbox.AddChild(_descriptionLabel);
        _textVbox.AddChild(_percentageLabel);

        _hbox.AddChild(_iconRect);
        _hbox.AddChild(_textVbox);

        _panel.AddChild(_hbox);
        AddChild(_panel);

        // Initially position offscreen
        HorizontalAlignment = HAlignment.Right;
        VerticalAlignment = VAlignment.Bottom;
    }

    public void AnimationStart(CEAchievementPrototype achievement, float percentage)
    {
        // Set achievement data
        _descriptionLabel.SetMessage($"[bold]{Loc.GetString(achievement.Name)}[/bold]\n{Loc.GetString(achievement.Description)}");
        _percentageLabel.Text = Loc.GetString("ce-achievements-percentage", ("percent", percentage.ToString("F1")));

        // Set icon
        try
        {
            _iconRect.Texture = _spriteSystem.Frame0(achievement.UnlockedIcon);
        }
        catch
        {
            // Fallback if texture not found
            _iconRect.Texture = null;
        }

        // Calculate positions for animation using margin
        _onscreenMarginRight = 20f; // Normal margin from right edge
        _offscreenMarginRight = -350f; // Start offscreen to the right (negative to move it off right edge)

        // Start animation
        _elapsedTime = 0f;
        _animationState = AnimationState.SlideIn;
        Margin = new Thickness(0, 0, _offscreenMarginRight, 20);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _elapsedTime += args.DeltaSeconds;

        switch (_animationState)
        {
            case AnimationState.SlideIn:
                if (_elapsedTime >= SlideDuration)
                {
                    Margin = new Thickness(0, 0, _onscreenMarginRight, 20);
                    _elapsedTime = 0f;
                    _animationState = AnimationState.Display;
                }
                else
                {
                    var progress = _elapsedTime / SlideDuration;
                    var smoothProgress = SmoothStep(progress);
                    var currentMarginRight = MathHelper.Lerp(_offscreenMarginRight, _onscreenMarginRight, smoothProgress);
                    Margin = new Thickness(0, 0, currentMarginRight, 20);
                }
                break;

            case AnimationState.Display:
                if (_elapsedTime >= DisplayTime)
                {
                    _elapsedTime = 0f;
                    _animationState = AnimationState.SlideOut;
                }
                break;

            case AnimationState.SlideOut:
                if (_elapsedTime >= SlideDuration)
                {
                    OnAnimationEnd?.Invoke();
                }
                else
                {
                    var progress = _elapsedTime / SlideDuration;
                    var smoothProgress = SmoothStep(progress);
                    var currentMarginRight = MathHelper.Lerp(_onscreenMarginRight, _offscreenMarginRight, smoothProgress);
                    Margin = new Thickness(0, 0, currentMarginRight, 20);
                }
                break;
        }
    }

    /// <summary>
    /// Smooth step function for easing animation (equivalent to smoothstep in GLSL)
    /// </summary>
    private static float SmoothStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
