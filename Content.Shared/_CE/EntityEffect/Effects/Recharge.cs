using Content.Shared._CE.Charges;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Fully restores all charges on the target entity.
/// </summary>
public sealed partial class Recharge : CEEntityEffectBase<Recharge>
{
}

public sealed partial class CERechargeEffectSystem : CEEntityEffectSystem<Recharge>
{
    [Dependency] private readonly CEChargesSystem _charges = default!;

    protected override void Effect(ref CEEntityEffectEvent<Recharge> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _charges.RestoreFull(entity);
    }
}
