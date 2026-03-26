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
        if (args.Args.Target is null)
            return;

        _health.Heal(args.Args.Target.Value, args.Effect.Amount, args.Args.User);
    }
}
