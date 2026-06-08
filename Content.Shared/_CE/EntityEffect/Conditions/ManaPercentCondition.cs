using Content.Shared._CE.Mana.Core.Components;

namespace Content.Shared._CE.EntityEffect.Conditions;

/// <summary>
/// Passes when the target entity's mana ratio is within [Min, Max] (0.0–1.0).
/// Fails if the entity has no mana component.
/// </summary>
public sealed partial class ManaPercentCondition : CEEntityConditionBase<ManaPercentCondition>
{
    /// <summary>Minimum mana ratio (inclusive). Null = no lower bound.</summary>
    [DataField]
    public float? Min;

    /// <summary>Maximum mana ratio (inclusive). Null = no upper bound.</summary>
    [DataField]
    public float? Max;
}

public sealed partial class ManaPercentConditionSystem : CEEntityConditionSystem<ManaPercentCondition>
{
    [Dependency] private EntityQuery<CEMagicEnergyContainerComponent> _manaQuery = default!;

    protected override void Condition(ref CEEntityConditionEvent<ManaPercentCondition> args)
    {
        if (!_manaQuery.TryComp(args.Entity, out var mana) || mana.MaxEnergy <= 0)
            return;

        var ratio = (float) mana.Energy / mana.MaxEnergy;

        if (args.Condition.Min is { } min && ratio < min)
            return;

        if (args.Condition.Max is { } max && ratio > max)
            return;

        args.Result = true;
    }
}
