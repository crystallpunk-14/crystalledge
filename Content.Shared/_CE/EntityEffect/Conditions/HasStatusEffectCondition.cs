using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Conditions;

/// <summary>
/// Passes when the target entity has a specific status effect active.
/// </summary>
public sealed partial class HasStatusEffectCondition : CEEntityConditionBase<HasStatusEffectCondition>
{
    [DataField(required: true)]
    public EntProtoId StatusEffect;
}

public sealed partial class HasStatusEffectConditionSystem : CEEntityConditionSystem<HasStatusEffectCondition>
{
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    protected override void Condition(ref CEEntityConditionEvent<HasStatusEffectCondition> args)
    {
        args.Result = _statusEffect.HasStatusEffect(args.Entity, args.Condition.StatusEffect);
    }
}
