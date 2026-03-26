using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Data-only effect that spawns a client-side visual entity with customizable animations.
/// Logic is handled by client-side <c>CEEntityAnimationEffectSystem</c>.
/// Server-side, the animation system sends a network event for non-predicting clients.
/// </summary>
public sealed partial class EntityAnimation : CEEntityEffectBase<EntityAnimation>
{
    public EntityAnimation()
    {
        EffectTarget = CEEffectTarget.User;
    }

    /// <summary>
    /// Clientside VFX entity that visuals we spawn and animate. If null, the sprite is copied from the used item entity.
    /// </summary>
    [DataField]
    public EntProtoId? DummyEntity;

    /// <summary>
    /// Whether the spawned visual entity should follow the user's position.
    /// </summary>
    [DataField]
    public bool FollowUser = true;

    /// <summary>
    /// Keyframes for animating the sprite's offset position over time.
    /// If empty, sprite stays at the initial offset position.
    /// </summary>
    [DataField]
    public List<CEOffsetKeyframe> OffsetAnimation = new();

    /// <summary>
    /// Keyframes for animating the sprite's rotation over time.
    /// If empty, sprite stays at the initial rotation.
    /// </summary>
    [DataField]
    public List<CERotationKeyframe> RotationAnimation = new();

    /// <summary>
    /// Keyframes for animating the sprite's color/alpha over time.
    /// If empty, sprite stays at full opacity. Set alpha to 0 for fade-out effects.
    /// </summary>
    [DataField]
    public List<CEColorKeyframe> ColorAnimation = new();

    /// <summary>
    /// Keyframes for animating the sprite's scale over time.
    /// If empty, sprite stays at default (1, 1) scale
    /// </summary>
    [DataField]
    public List<CEScaleKeyFrame> ScaleAnimation = new();
}

/// <summary>
/// Network event sent to non-predicting clients to display visual effects
/// that were already processed on the predicting client.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEEntityAnimationEvent(NetEntity entity, NetEntity? used, Angle angle, TimeSpan frame) : EntityEventArgs
{
    public NetEntity Entity = entity;
    public NetEntity? Used = used;
    public Angle Angle = angle;
    public TimeSpan Frame = frame;
}


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
