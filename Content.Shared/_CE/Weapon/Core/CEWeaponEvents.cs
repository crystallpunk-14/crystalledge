using System.Numerics;
using Content.Shared._CE.Weapon.Core.Components;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Weapon.Core;

/// <summary>
/// Network event sent from client to server when performing an attack.
/// Contains all data needed for both precise and wide attacks.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEWeaponAttackEvent : EntityEventArgs
{
    /// <summary>
    /// Coordinates being attacked.
    /// </summary>
    public readonly NetCoordinates Coordinates;

    /// <summary>
    /// The weapon entity being used.
    /// </summary>
    public readonly NetEntity Weapon;

    /// <summary>
    /// Which button binding triggered this attack.
    /// </summary>
    public readonly CEAttackType AttackType;

    /// <summary>
    /// Target entity for precise attacks.
    /// </summary>
    public readonly NetEntity? Target;

    /// <summary>
    /// Entity list for wide attacks (client-side arc raycasted).
    /// </summary>
    public readonly List<NetEntity> Entities;

    public CEWeaponAttackEvent(
        NetCoordinates coordinates,
        NetEntity weapon,
        CEAttackType attackType,
        NetEntity? target = null,
        List<NetEntity>? entities = null)
    {
        Coordinates = coordinates;
        Weapon = weapon;
        AttackType = attackType;
        Target = target;
        Entities = entities ?? new List<NetEntity>();
    }
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
/// Raised directed at a user to check whether they are allowed to attack a target.
/// Combat will also check the general interaction blockers, so this event should only
/// be used for CE combat-specific action blocking.
/// </summary>
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
/// Raised directed on a weapon when attempting an attack. Can be cancelled.
/// </summary>
[ByRefEvent]
public record struct CEWeaponAttackAttemptEvent(EntityUid User, bool Cancelled = false, string? Message = null);

/// <summary>
/// Raised when a client releases the attack button.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEStopAttackEvent(NetEntity weapon) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
}

/// <summary>
/// Event raised on the user after attacking with a weapon, regardless of whether it hit anything.
/// </summary>
[ByRefEvent]
public record struct CEAfterAttackEvent(EntityUid Weapon);

/// <summary>
/// Raised directed on a weapon before damage is applied.
/// Allows modifying damage, adding modifiers, or cancelling the hit.
/// </summary>
public sealed class CEMeleeHitEvent : HandledEntityEventArgs
{
    public readonly DamageSpecifier BaseDamage;
    public List<DamageModifierSet> ModifiersList = new();
    public DamageSpecifier BonusDamage = new();
    public IReadOnlyList<EntityUid> HitEntities;
    public SoundSpecifier? HitSoundOverride;
    public readonly EntityUid User;
    public readonly EntityUid Weapon;
    public readonly Vector2? Direction;
    public bool IsHit = true;

    public CEMeleeHitEvent(
        List<EntityUid> hitEntities,
        EntityUid user,
        EntityUid weapon,
        DamageSpecifier baseDamage,
        Vector2? direction)
    {
        HitEntities = hitEntities;
        User = user;
        Weapon = weapon;
        BaseDamage = baseDamage;
        Direction = direction;
    }
}

/// <summary>
/// Raised on targets that were attacked. Allows adding bonus damage.
/// </summary>
public sealed class CEAttackedEvent : EntityEventArgs
{
    public EntityUid Used { get; }
    public EntityUid User { get; }
    public EntityCoordinates ClickLocation { get; }
    public DamageSpecifier BonusDamage = new();

    public CEAttackedEvent(EntityUid used, EntityUid user, EntityCoordinates clickLocation)
    {
        Used = used;
        User = user;
        ClickLocation = clickLocation;
    }
}

/// <summary>
/// Server-to-client event for lunge animation data.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEMeleeLungeEvent : EntityEventArgs
{
    public NetEntity Entity;
    public NetEntity Weapon;
    public Angle Angle;
    public Vector2 LocalPos;
    public string? Animation;

    public CEMeleeLungeEvent(
        NetEntity entity,
        NetEntity weapon,
        Angle angle,
        Vector2 localPos,
        string? animation)
    {
        Entity = entity;
        Weapon = weapon;
        Angle = angle;
        LocalPos = localPos;
        Animation = animation;
    }
}
