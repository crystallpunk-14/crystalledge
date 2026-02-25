using Content.Shared._CE.Weapon.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Weapon.Core;


/// <summary>
/// Network event sent from client to server when performing an attack.
/// Contains all data needed for both precise and wide attacks.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEWeaponAttackEvent(
    Angle angle,
    NetEntity weapon,
    CEAttackType attackType)
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
    public readonly CEAttackType AttackType = attackType;
}

/// <summary>
/// Event raised on entity in GetWeapon function to allow systems to manually
/// specify what the weapon should be.
/// </summary>
public sealed class CEGetMeleeWeaponEvent : HandledEntityEventArgs
{
    public Entity<CEWeaponComponent>? Weapon;
}

/// <summary>
/// Raised when a client releases the attack button.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEStopAttackEvent(NetEntity weapon) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
}
