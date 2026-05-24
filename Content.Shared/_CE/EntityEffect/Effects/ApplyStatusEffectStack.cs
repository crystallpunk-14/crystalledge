using Content.Shared._CE.StatusEffects.Core;
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
    public int Amount = 1;

    /// <summary>
    /// Maximum number of stacks that can be applied. 0 means no limit.
    /// </summary>
    [DataField]
    public int Max;

    /// <summary>
    /// When true, suppresses <see cref="CEAfterApplyStatusEffectEvent"/> after this application.
    /// Use to prevent skill proc loops where a skill applies the same status effect it listens for.
    /// </summary>
    [DataField]
    public bool SuppressEvents;
}

public sealed partial class CEApplyStatusEffectStackEffectSystem : CEEntityEffectSystem<ApplyStatusEffectStack>
{
    [Dependency] private readonly CEStatusEffectStackSystem _effectStack = default!;

    protected override void Effect(ref CEEntityEffectEvent<ApplyStatusEffectStack> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _effectStack.TryAddStack(entity, args.Effect.StatusEffect, out _, args.Effect.Amount, source: args.Args.Source, max: args.Effect.Max, used: args.Args.Used, suppressEvents: args.Effect.SuppressEvents);
    }
}

