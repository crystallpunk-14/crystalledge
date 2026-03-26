using Content.Shared._CE.Health;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class Heal : CEEntityEffectBase<Heal>
{
    [DataField]
    public int Amount = 1;
}

public sealed partial class CEHealEffectSystem : CEEntityEffectSystem<Heal>
{
    [Dependency] private readonly CESharedDamageableSystem _health = default!;

    protected override void Effect(ref CEEntityEffectEvent<Heal> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _health.Heal(entity, args.Effect.Amount, args.Args.User);
    }
}
