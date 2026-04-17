using Content.Shared._CE.StatusEffectStacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Converts stacks of one status effect into another.
/// Removes source stacks and adds target stacks based on the ratio.
/// </summary>
public sealed partial class StatusEffectConversion : CEEntityEffectBase<StatusEffectConversion>
{
    /// <summary>
    /// The status effect to convert from.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId SourceEffect;

    /// <summary>
    /// The status effect to convert into.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId TargetEffect;

    /// <summary>
    /// How many source stacks are needed per 1 target stack.
    /// </summary>
    [DataField]
    public float Ratio = 1f;

    /// <summary>
    /// Maximum number of source stacks to convert. 0 means no limit.
    /// </summary>
    [DataField]
    public int MaxConversion;
}

public sealed partial class CEStatusEffectConversionSystem : CEEntityEffectSystem<StatusEffectConversion>
{
    [Dependency] private readonly CEStatusEffectStackSystem _effectStack = default!;

    protected override void Effect(ref CEEntityEffectEvent<StatusEffectConversion> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        var currentSource = _effectStack.GetStack(entity, args.Effect.SourceEffect);
        if (currentSource <= 0)
            return;

        var toConvert = currentSource;
        if (args.Effect.MaxConversion > 0)
            toConvert = Math.Min(toConvert, args.Effect.MaxConversion);

        var targetStacks = (int)(toConvert / args.Effect.Ratio);
        if (targetStacks <= 0)
            return;

        // Only remove the source stacks that were actually converted.
        var actualRemoved = (int)(targetStacks * args.Effect.Ratio);

        _effectStack.TryRemoveStack(entity, args.Effect.SourceEffect, actualRemoved);
        _effectStack.TryAddStack(entity, args.Effect.TargetEffect, out _, targetStacks);
    }
}
