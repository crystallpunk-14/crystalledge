using Content.Shared._CE.Stamina;

namespace Content.Shared._CE.EntityEffect.Conditions;

/// <summary>
/// Passes when the target entity is exhausted (stamina depleted to zero).
/// Fails if the entity has no stamina component.
/// </summary>
public sealed partial class ExhaustedCondition : CEEntityConditionBase<ExhaustedCondition>
{
}

public sealed partial class ExhaustedConditionSystem : CEEntityConditionSystem<ExhaustedCondition>
{
    [Dependency] private EntityQuery<CEStaminaComponent> _staminaQuery = default!;

    protected override void Condition(ref CEEntityConditionEvent<ExhaustedCondition> args)
    {
        if (_staminaQuery.TryComp(args.Entity, out var comp) && comp.Exhausted)
            args.Result = true;
    }
}
