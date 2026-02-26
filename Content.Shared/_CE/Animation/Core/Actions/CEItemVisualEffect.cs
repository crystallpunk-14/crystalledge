using Robust.Shared.Utility;

namespace Content.Shared._CE.Animation.Core.Actions;

/// <summary>
/// A CEAnimationActionEntry that spawns a client-side visual entity resembling the used item
/// (or with an overridden sprite), plays an animation on it, and despawns it.
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
    /// Whether the visual entity should fade out over its lifetime.
    /// </summary>
    [DataField]
    public bool FadeOut = true;

    /// <summary>
    /// Whether the spawned visual entity should follow the user's position.
    /// </summary>
    [DataField]
    public bool FollowUser = true;

    /// <summary>
    /// Positional offset from the user in the direction of the animation angle.
    /// </summary>
    [DataField]
    public float Offset = 0.5f;

    /// <summary>
    /// Optional rotation to apply to the sprite (in degrees).
    /// </summary>
    [DataField]
    public float SpriteRotation;

    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle)
    {
        //Check out client implementatino
    }
}
