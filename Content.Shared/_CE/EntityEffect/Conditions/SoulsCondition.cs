using Content.Shared._CE.Soul.Components;

namespace Content.Shared._CE.EntityEffect.Conditions;

/// <summary>
/// Passes when the target entity's soul count is within [Min, Max].
/// Returns 0 souls if the entity has no soul container.
/// </summary>
public sealed partial class SoulsCondition : CEEntityConditionBase<SoulsCondition>
{
    /// <summary>Minimum soul count (inclusive). Null = no lower bound.</summary>
    [DataField]
    public int? Min;

    /// <summary>Maximum soul count (inclusive). Null = no upper bound.</summary>
    [DataField]
    public int? Max;
}

public sealed partial class SoulsConditionSystem : CEEntityConditionSystem<SoulsCondition>
{
    [Dependency] private readonly EntityQuery<CESoulContainerComponent> _soulQuery = default!;

    protected override void Condition(ref CEEntityConditionEvent<SoulsCondition> args)
    {
        _soulQuery.TryComp(args.Entity, out var souls);
        var count = souls?.Souls ?? 0;

        if (args.Condition.Min is { } min && count < min)
            return;

        if (args.Condition.Max is { } max && count > max)
            return;

        args.Result = true;
    }
}
