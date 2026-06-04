using Content.Shared._CE.Stamina;

namespace Content.Shared._CE.EntityEffect.Conditions;

/// <summary>
/// Passes when the target entity's stamina ratio is within [Min, Max] (0.0–1.0).
/// Fails if the entity has no stamina component.
/// </summary>
public sealed partial class StaminaPercentCondition : CEEntityConditionBase<StaminaPercentCondition>
{
    /// <summary>Minimum stamina ratio (inclusive). Null = no lower bound.</summary>
    [DataField]
    public float? Min;

    /// <summary>Maximum stamina ratio (inclusive). Null = no upper bound.</summary>
    [DataField]
    public float? Max;
}

public sealed partial class StaminaPercentConditionSystem : CEEntityConditionSystem<StaminaPercentCondition>
{
    [Dependency] private readonly CEStaminaSystem _stamina = default!;
    [Dependency] private readonly EntityQuery<CEStaminaComponent> _staminaQuery = default!;

    protected override void Condition(ref CEEntityConditionEvent<StaminaPercentCondition> args)
    {
        if (!_staminaQuery.TryComp(args.Entity, out var comp) || comp.MaxStamina <= 0f)
            return;

        var ratio = _stamina.GetStamina((args.Entity, comp)) / comp.MaxStamina;

        if (args.Condition.Min is { } min && ratio < min)
            return;

        if (args.Condition.Max is { } max && ratio > max)
            return;

        args.Result = true;
    }
}
