using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;

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

    [DataField]
    public bool IgnoreArmor;
}

public sealed partial class CEDamageEffectSystem : CEEntityEffectSystem<Damage>
{
    [Dependency] private readonly CESharedDamageableSystem _health = default!;

    protected override void Effect(ref CEEntityEffectEvent<Damage> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        if (args.Effect.IgnoreArmor)
            _health.ChangeDamage(entity, args.Effect.DamageSpec.Total, out _);
        else
            _health.TakeDamage(entity, args.Effect.DamageSpec);
    }
}
