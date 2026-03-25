using Content.Shared._CE.Health;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Deals typed damage to the user (self), going through armor and modifiers.
/// </summary>
public sealed partial class SelfDamage : CEEntityEffectBase<SelfDamage>
{
    [DataField(required: true)]
    public CEDamageSpecifier Damage = new();
}

public sealed partial class CESelfDamageEffectSystem : CEEntityEffectSystem<SelfDamage>
{
    [Dependency] private readonly CESharedDamageableSystem _health = default!;

    protected override void Effect(ref CEEntityEffectEvent<SelfDamage> args)
    {
        _health.TakeDamage(args.Args.User, args.Effect.Damage);
    }
}
