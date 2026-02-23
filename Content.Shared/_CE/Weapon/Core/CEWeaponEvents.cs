using Content.Shared._CE.Weapon.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Weapon.Core;


public abstract class CEWeaponAttackEvent(NetCoordinates coordinates, NetEntity weapon) : EntityEventArgs
{
    /// <summary>
    /// Coordinates being attacked.
    /// </summary>
    public readonly NetCoordinates Coordinates = coordinates;
    public readonly NetEntity Weapon = weapon;
}

public sealed class CEWeaponWideAttackEvent(NetCoordinates coordinates, NetEntity weapon) : CEWeaponAttackEvent(coordinates, weapon);


/// <summary>
/// Event raised on entity in GetWeapon function to allow systems to manually
/// specify what the weapon should be.
/// </summary>
public sealed class CEGetMeleeWeaponEvent : HandledEntityEventArgs
{
    public Entity<CEWeaponComponent>? Weapon;
}

/// <summary>
///     Raised Directed at a user to check whether they are allowed to attack a target.
/// </summary>
/// <remarks>
///     Combat will also check the general interaction blockers, so this event should only be used for combat-specific
///     action blocking.
/// </remarks>
public sealed class CEAttackAttemptEvent(
    EntityUid user,
    EntityUid? target = null,
    Entity<CEWeaponComponent>? weapon = null)
    : CancellableEntityEventArgs
{
    public EntityUid User { get; } = user;
    public EntityUid? Target { get; } = target;

    public Entity<CEWeaponComponent>? Weapon { get; } = weapon;
}

/// <summary>
/// Raised directed on a weapon when attempt a melee attack.
/// </summary>
[ByRefEvent]
public sealed class CEWeaponAttackAttemptEvent(
    EntityUid user,
    EntityUid? target = null,
    Entity<CEWeaponComponent>? weapon = null)
    : CancellableEntityEventArgs
{
    public EntityUid User { get; } = user;
    public EntityUid? Target { get; } = target;

    public Entity<CEWeaponComponent>? Weapon { get; } = weapon;
}


/// <summary>
/// TODO
/// </summary>
[Serializable, NetSerializable]
public sealed class CEStopAttackEvent(NetEntity weapon, CEAttackType attackType) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
    public readonly CEAttackType AttackType = attackType;
}

/// <summary>
/// Event raised on the user after attacking with a weapon, regardless of whether it hit anything.
/// </summary>
[ByRefEvent]
public sealed class CEAfterAttackEvent(EntityUid Weapon);
