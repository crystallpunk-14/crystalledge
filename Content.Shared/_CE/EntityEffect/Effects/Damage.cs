using Content.Shared._CE.Health;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Deals typed damage to the resolved target entity, going through armor and modifiers.
/// Defaults to <see cref="CEEffectTarget.User"/> for backward compatibility.
/// </summary>
public sealed partial class Damage : CEEntityEffectBase<Damage>
{
    public Damage()
    {
        EffectTarget = CEEffectTarget.User;
    }

    [DataField("damage", required: true)]
    public CEDamageSpecifier DamageSpec = new();
}

public sealed partial class CEDamageEffectSystem : CEEntityEffectSystem<Damage>
{
    [Dependency] private readonly CESharedDamageableSystem _health = default!;

    protected override void Effect(ref CEEntityEffectEvent<Damage> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _health.TakeDamage(entity, args.Effect.DamageSpec);
    }
}
