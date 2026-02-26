using Robust.Shared.Serialization;

namespace Content.Shared._CE.Weapon;

/// <summary>
/// Raised on the server and sent to clients to play melee attack visual effects.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEMeleeAttackEffectEvent : EntityEventArgs
{
    /// <summary>
    /// The user who performed the attack.
    /// </summary>
    public NetEntity User;

    /// <summary>
    /// List of entities that were hit by the attack.
    /// </summary>
    public List<NetEntity> Targets;

    public CEMeleeAttackEffectEvent(NetEntity user, List<NetEntity> targets)
    {
        User = user;
        Targets = targets;
    }
}
