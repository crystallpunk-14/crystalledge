using Content.Shared._CE.Animation.Item.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Animation.Item;

/// <summary>
/// Network event sent from client to server when performing an attack.
/// Contains all data needed for both precise and wide attacks.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEItemAnimationUseEvent(
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
/// Event raised on entity in GetWeapon function to allow systems to manually
/// specify what the weapon should be.
/// </summary>
public sealed class CEGetItemAnimationEvent : HandledEntityEventArgs
{
    public Entity<CEItemAnimationComponent>? Weapon;
}

/// <summary>
/// Raised when a client releases the attack button.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEStopItemAnimationUseEvent(NetEntity weapon) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
}
