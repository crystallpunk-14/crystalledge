using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Animation.Core.Actions;

/// <summary>
/// A CEAnimationActionEntry that spawns a client-side visual entity resembling the used item
/// (or with an overridden sprite), plays a customizable animation on it, and despawns it.
/// Server-side this is a no-op; client-side the partial method provides the visual implementation.
/// </summary>
public abstract partial class SharedItemVisualEffect : CEAnimationActionEntry
{
    /// <summary>
    /// Optional sprite override. If null, the sprite is copied from the used item entity.
    /// </summary>
    [DataField]
    public SpriteSpecifier? SpriteOverride;

    /// <summary>
    /// The RSI path for the animation to play on the spawned entity. If null, the default item sprite is used as-is.
    /// </summary>
    [DataField]
    public ResPath? AnimationRsi;

    /// <summary>
    /// The RSI state to play on the spawned entity. If null, no flick animation is played.
    /// </summary>
    [DataField]
    public string? AnimationState;

    /// <summary>
    /// Duration of the visual effect in seconds.
    /// </summary>
    [DataField]
    public float Duration = 0.5f;

    /// <summary>
    /// Whether the spawned visual entity should follow the user's position.
    /// </summary>
    [DataField]
    public bool FollowUser = true;

    /// <summary>
    /// Initial rotation to apply to the sprite (in degrees), added to the animation angle.
    /// This is used as the starting rotation if no rotation animation is specified.
    /// </summary>
    [DataField]
    public float SpriteRotation;

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

    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle, TimeSpan frame)
    {
        //Check out client implementation
    }
}
