using Content.Shared._CE.Animation.Item.Components;

namespace Content.Shared._CE.MeleeWeapon;

/// <summary>
/// Raised on the weapon entity before a weapon use is committed.
/// Cost components subscribe to this and set <see cref="Cancelled"/> if resources are insufficient.
/// </summary>
public sealed class CEWeaponUseAttemptEvent : CancellableEntityEventArgs
{
    public EntityUid User;
    public CEUseType UseType;

    public CEWeaponUseAttemptEvent(EntityUid user, CEUseType useType)
    {
        User = user;
        UseType = useType;
    }
}

/// <summary>
/// Raised on the weapon entity after a weapon use is committed.
/// Cost components subscribe to this to consume resources.
/// </summary>
public sealed class CEWeaponUsedEvent : EntityEventArgs
{
    public EntityUid User;
    public CEUseType UseType;

    public CEWeaponUsedEvent(EntityUid user, CEUseType useType)
    {
        User = user;
        UseType = useType;
    }
}
