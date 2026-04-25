using Content.Shared._CE.StatusEffectStacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Applies a status effect with stack accumulation. Each application adds stacks
/// rather than just refreshing the duration.
/// </summary>
public sealed partial class ApplyStatusEffectStack : CEEntityEffectBase<ApplyStatusEffectStack>
{
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    [DataField]
    public TimeSpan? Duration;

    [DataField]
    public int Amount = 1;

    /// <summary>
    /// Maximum number of stacks that can be applied. 0 means no limit.
    /// </summary>
    [DataField]
    public int Max;
}

public sealed partial class CEApplyStatusEffectStackEffectSystem : CEEntityEffectSystem<ApplyStatusEffectStack>
{
    [Dependency] private readonly CEStatusEffectStackSystem _effectStack = default!;

    protected override void Effect(ref CEEntityEffectEvent<ApplyStatusEffectStack> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        var stacks = args.Effect.Amount;
        if (args.Effect.Max > 0)
        {
            var current = _effectStack.GetStack(entity, args.Effect.StatusEffect);
            stacks = Math.Min(stacks, args.Effect.Max - current);

            if (stacks <= 0)
                return;
        }

        if (!_effectStack.TryAddStack(entity, args.Effect.StatusEffect, out var statusEnt, stacks, args.Effect.Duration))
            return;

        if (statusEnt == null || !Exists(args.Args.Source))
            return;

        var sourceComp = EnsureComp<CEStatusEffectSourceComponent>(statusEnt.Value);
        sourceComp.Source = args.Args.Source;
        Dirty(statusEnt.Value, sourceComp);
    }
}
