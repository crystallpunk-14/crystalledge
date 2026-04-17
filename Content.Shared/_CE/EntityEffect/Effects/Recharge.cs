using Content.Shared._CE.Charges;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Restores charges on the target entity by a percentage of its max charges.
/// </summary>
public sealed partial class Recharge : CEEntityEffectBase<Recharge>
{
    /// <summary>
    /// Fraction of max charges to restore. 1.0 = 100%, 0.5 = 50%.
    /// </summary>
    [DataField]
    public float Percentage = 1f;
}

public sealed partial class CERechargeEffectSystem : CEEntityEffectSystem<Recharge>
{
    [Dependency] private readonly CEChargesSystem _charges = default!;

    protected override void Effect(ref CEEntityEffectEvent<Recharge> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _charges.RestorePercentage(entity, args.Effect.Percentage);
    }
}
