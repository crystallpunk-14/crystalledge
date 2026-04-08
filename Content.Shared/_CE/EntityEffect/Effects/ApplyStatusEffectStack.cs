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

    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.FromSeconds(1f);

    [DataField]
    public int Stack = 1;
}

public sealed partial class CEApplyStatusEffectStackEffectSystem : CEEntityEffectSystem<ApplyStatusEffectStack>
{
    [Dependency] private readonly CEStatusEffectStackSystem _effectStack = default!;

    protected override void Effect(ref CEEntityEffectEvent<ApplyStatusEffectStack> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _effectStack.TryAddStack(entity, args.Effect.StatusEffect, out _, args.Effect.Stack, args.Effect.Duration);
    }
}
