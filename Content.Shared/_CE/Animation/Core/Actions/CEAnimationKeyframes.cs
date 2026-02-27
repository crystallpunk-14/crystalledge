using System.Numerics;

namespace Content.Shared._CE.Animation.Core.Actions;

/// <summary>
/// Defines a keyframe for animating sprite offset (position relative to entity).
/// </summary>
[DataDefinition]
public sealed partial class CEOffsetKeyframe
{
    /// <summary>
    /// Time in seconds from the start of the animation when this keyframe is reached.
    /// </summary>
    [DataField(required: true)]
    public float Time;

    /// <summary>
    /// The sprite offset at this keyframe. X and Y are in world units.
    /// </summary>
    [DataField]
    public Vector2 Offset = Vector2.Zero;

    /// <summary>
    /// Easing function to use when transitioning to this keyframe.
    /// </summary>
    [DataField]
    public CEAnimationEasing Easing = CEAnimationEasing.Linear;
}

/// <summary>
/// Defines a keyframe for animating sprite rotation.
/// </summary>
[DataDefinition]
public sealed partial class CERotationKeyframe
{
    /// <summary>
    /// Time in seconds from the start of the animation when this keyframe is reached.
    /// </summary>
    [DataField(required: true)]
    public float Time;

    /// <summary>
    /// The sprite rotation at this keyframe in degrees.
    /// </summary>
    [DataField]
    public float Rotation;

    /// <summary>
    /// Easing function to use when transitioning to this keyframe.
    /// </summary>
    [DataField]
    public CEAnimationEasing Easing = CEAnimationEasing.Linear;
}

/// <summary>
/// Defines a keyframe for animating sprite color/alpha (for fade effects).
/// </summary>
[DataDefinition]
public sealed partial class CEColorKeyframe
{
    /// <summary>
    /// Time in seconds from the start of the animation when this keyframe is reached.
    /// </summary>
    [DataField(required: true)]
    public float Time;

    /// <summary>
    /// The color/alpha value at this keyframe. Use alpha channel for fade effects.
    /// </summary>
    [DataField]
    public Color Color = Color.White;

    /// <summary>
    /// Easing function to use when transitioning to this keyframe.
    /// </summary>
    [DataField]
    public CEAnimationEasing Easing = CEAnimationEasing.Linear;
}

/// <summary>
/// Defines a keyframe for animating sprite scale.
/// </summary>
[DataDefinition]
public sealed partial class CEScaleKeyFrame
{
    /// <summary>
    /// Time in seconds from the start of the animation when this keyframe is reached.
    /// </summary>
    [DataField(required: true)]
    public float Time;

    [DataField]
    public Vector2 Scale = Vector2.One;

    /// <summary>
    /// Easing function to use when transitioning to this keyframe.
    /// </summary>
    [DataField]
    public CEAnimationEasing Easing = CEAnimationEasing.Linear;
}

/// <summary>
/// Defines easing functions for animation interpolation.
/// </summary>
public enum CEAnimationEasing : byte
{
    Linear,
    QuadIn,
    QuadOut,
    QuadInOut,
    CubicIn,
    CubicOut,
    CubicInOut,
    QuartIn,
    QuartOut,
    QuartInOut,
}
