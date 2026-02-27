using Content.Shared._CE.Animation.Core.Prototypes;
using Content.Shared._CE.Animation.Item.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Animation.Core;

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

/// <summary>
/// Local event raised when an ArcAttack fires, used for debug visualization.
/// </summary>
public sealed class CEItemAttackEvent(MapCoordinates position, Angle direction, float range, float arcWidth)
    : EntityEventArgs
{
    public MapCoordinates Position = position;
    public Angle Direction = direction;
    public float Range = range;
    public float ArcWidth = arcWidth;
}

/// <summary>
/// Is called on the object being used to determine what animations it provides
/// </summary>
public sealed class CEGetItemAnimationsEvent(Entity<CEItemAnimationComponent> used, CEUseType useType) : HandledEntityEventArgs
{
    public Entity<CEItemAnimationComponent> Used = used;
    public CEUseType UseType = useType;
    public List<CEAnimationEntry> Animations = new();
}
