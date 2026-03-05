using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Animation.Core.Actions;

/// <summary>
/// A CEAnimationActionEntry that spawns a client-side visual entity resembling the used item
/// (or with an overridden sprite), plays a customizable animation on it, and despawns it.
/// Server-side this is a no-op; client-side the partial method provides the visual implementation.
/// </summary>
public abstract partial class SharedEntityAnimation : CEAnimationActionEntry
{
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
