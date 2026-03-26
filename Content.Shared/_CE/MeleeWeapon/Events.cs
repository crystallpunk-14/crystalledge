using Content.Shared._CE.Animation.Item.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.MeleeWeapon;

/// <summary>
/// Is called on the object being used to determine what animations it provides
/// </summary>
public sealed class CEGetWeaponEvent(Entity<CEWeaponComponent> used, CEUseType useType) : HandledEntityEventArgs
{
    public Entity<CEWeaponComponent> Used = used;
    public CEUseType UseType = useType;
    public List<CEAnimationEntry> Animations = new();
}

/// <summary>
/// Network event sent from client to server when performing an attack.
/// Contains all data needed for both precise and wide attacks.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEWeaponUseEvent(
    Angle angle,
    NetEntity weapon,
    CEUseType useType)
    : EntityEventArgs
{
    /// <summary>
    /// Angle being attacked.
    /// </summary>
    public readonly Angle Angle = angle;

    /// <summary>
    /// The weapon entity being used.
    /// </summary>
    public readonly NetEntity Weapon = weapon;

    /// <summary>
    /// Which button binding triggered this attack.
    /// </summary>
    public readonly CEUseType UseType = useType;
}

/// <summary>
/// Sent from client to server at the arc attack keyframe.
/// Contains the client's calculated hit list for server validation.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEWeaponArcHitEvent(
    NetEntity weapon,
    List<NetEntity> targets,
    float power)
    : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
    public readonly List<NetEntity> Targets = targets;
    public readonly float Power = power;
}

/// <summary>
/// Event raised on entity in GetWeapon function to allow systems to manually
/// specify what the weapon should be.
/// </summary>
public sealed class CEGetAnimationItemForUseEvent : HandledEntityEventArgs
{
    public Entity<CEWeaponComponent>? Used;
}

/// <summary>
/// Raised when a client releases the attack button.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEStopWeaponUseEvent(NetEntity weapon) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
}

/// <summary>
/// It is called on both the item being used and the creature using the item before the animation starts, to calculate the animation's speed.
/// </summary>
public sealed class CEGetWeaponSpeedEvent : EntityEventArgs
{
    private float _multiplier = 1f;

    public void Modify(float multiplier)
    {
        _multiplier *= multiplier;
    }

    public float GetSpeed()
    {
        return _multiplier;
    }
}
