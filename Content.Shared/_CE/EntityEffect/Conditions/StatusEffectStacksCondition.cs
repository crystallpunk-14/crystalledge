using Content.Shared._CE.StatusEffects.Core;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Conditions;

/// <summary>
/// Passes when the stack count of a status effect on the target is within [Min, Max].
/// Null Min/Max means no bound. Returns false when the effect is absent and Min > 0.
/// </summary>
public sealed partial class StatusEffectStacksCondition : CEEntityConditionBase<StatusEffectStacksCondition>
{
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    [DataField]
    public int? Min;

    [DataField]
    public int? Max;
}

public sealed partial class StatusEffectStacksConditionSystem : CEEntityConditionSystem<StatusEffectStacksCondition>
{
    [Dependency] private CEStatusEffectStackSystem _effectStack = default!;

    protected override void Condition(ref CEEntityConditionEvent<StatusEffectStacksCondition> args)
    {
        var stacks = _effectStack.GetStack(args.Entity, args.Condition.StatusEffect);

        if (args.Condition.Min is { } min && stacks < min)
            return;

        if (args.Condition.Max is { } max && stacks > max)
            return;

        args.Result = true;
    }
}
