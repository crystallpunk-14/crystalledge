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

    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle, float animationSpeed, TimeSpan frame)
    {
        //Check out client implementation
    }
}
