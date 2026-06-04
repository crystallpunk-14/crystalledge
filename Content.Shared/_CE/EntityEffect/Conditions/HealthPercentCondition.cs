using Content.Shared._CE.Health;

namespace Content.Shared._CE.EntityEffect.Conditions;

/// <summary>
/// Passes when the target entity's health ratio is within [Min, Max] (0.0–1.0).
/// Fails if the entity has no health component.
/// </summary>
public sealed partial class HealthPercentCondition : CEEntityConditionBase<HealthPercentCondition>
{
    /// <summary>Minimum health ratio (inclusive). Null = no lower bound.</summary>
    [DataField]
    public float? Min;

    /// <summary>Maximum health ratio (inclusive). Null = no upper bound.</summary>
    [DataField]
    public float? Max;
}

public sealed partial class HealthPercentConditionSystem : CEEntityConditionSystem<HealthPercentCondition>
{
    [Dependency] private readonly CESharedDamageableSystem _health = default!;

    protected override void Condition(ref CEEntityConditionEvent<HealthPercentCondition> args)
    {
        var info = _health.GetHealthInfo(args.Entity);
        if (info.MaxHp <= 0)
            return;

        var ratio = info.Ratio;

        if (args.Condition.Min is { } min && ratio < min)
            return;

        if (args.Condition.Max is { } max && ratio > max)
            return;

        args.Result = true;
    }
}
