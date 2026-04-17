using Content.Shared._CE.Health;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Deals typed damage to the resolved target entity.
/// When <see cref="IgnoreArmor"/> is false (default), damage goes through armor and modifiers.
/// When true, damage bypasses all modifiers and is applied directly.
/// </summary>
public sealed partial class Damage : CEEntityEffectBase<Damage>
{
    [DataField("damage", required: true)]
    public CEDamageSpecifier DamageSpec = new();

    [DataField(required: true)]
    public CEAttackType AttackType;

    [DataField]
    public bool IgnoreArmor;

    [DataField]
    public bool InterruptDoAfters = true;
}

public sealed partial class CEDamageEffectSystem : CEEntityEffectSystem<Damage>
{
    [Dependency] private readonly CESharedDamageableSystem _health = default!;

    protected override void Effect(ref CEEntityEffectEvent<Damage> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        var damage = new CEDamageSpecifier(args.Effect.DamageSpec);

        var outgoingEv = new CEOutgoingDamageCalculateEvent(damage, entity, args.Args.Used, args.Effect.AttackType);
        RaiseLocalEvent(args.Args.Source, outgoingEv);

        if (outgoingEv.Cancelled)
            return;

        _health.TakeDamage(entity, outgoingEv.Damage, args.Args.Source, args.Args.Used, args.Effect.IgnoreArmor, args.Effect.InterruptDoAfters);
    }
}


/// <summary>
/// Determines the type of attack that deals damage.
/// </summary>
[Serializable, NetSerializable]
public enum CEAttackType : byte
{
    Melee,
    Ranged,
}

/// <summary>
/// Raised on the source (attacker) entity before damage is applied to the target.
/// Status effects on the attacker can modify damage via StatusEffectRelayedEvent.
/// </summary>
public sealed class CEOutgoingDamageCalculateEvent(
    CEDamageSpecifier damage,
    EntityUid target,
    EntityUid? weapon,
    CEAttackType attackType) : EntityEventArgs
{
    public CEDamageSpecifier Damage = damage;
    public EntityUid Target = target;
    public EntityUid? Weapon = weapon;
    public CEAttackType AttackType = attackType;
    public bool Cancelled;
}
