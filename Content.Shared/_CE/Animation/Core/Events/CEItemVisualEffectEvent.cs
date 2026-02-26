using Robust.Shared.Serialization;

namespace Content.Shared._CE.Animation.Core.Events;

/// <summary>
/// Network event sent to non-predicting clients to display visual effects
/// that were already processed on the predicting client.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEItemVisualEffectEvent(NetEntity entity, NetEntity? used, Angle angle, TimeSpan frame) : EntityEventArgs
{
    public NetEntity Entity = entity;
    public NetEntity? Used = used;
    public Angle Angle = angle;
    public TimeSpan Frame = frame;
}
