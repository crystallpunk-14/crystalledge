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

        // Raise on the source so that status effects on the attacker (e.g. pacifism) get a chance
        // to cancel the application via StatusEffectRelayedEvent.
        var attempt = new CEAttemptApplyStatusEffectStackEvent(entity, args.Effect.StatusEffect, stacks, args.Effect.Duration);
        if (Exists(args.Args.Source))
            RaiseLocalEvent(args.Args.Source, attempt);

        if (attempt.Cancelled)
            return;

        if (!_effectStack.TryAddStack(entity, args.Effect.StatusEffect, out var statusEnt, stacks, args.Effect.Duration))
            return;

        if (statusEnt == null || !Exists(args.Args.Source))
            return;

        var sourceComp = EnsureComp<CEStatusEffectSourceComponent>(statusEnt.Value);
        sourceComp.Source = args.Args.Source;
        Dirty(statusEnt.Value, sourceComp);
    }
}

/// <summary>
/// Raised on the source (attacker) before <see cref="ApplyStatusEffectStack"/> applies stacks to
/// a target. Cancelling prevents the stacks from being applied. Relayed to the source's active
/// status effects via <c>StatusEffectRelayedEvent</c>.
/// </summary>
public sealed class CEAttemptApplyStatusEffectStackEvent(
    EntityUid target,
    EntProtoId statusEffect,
    int amount,
    TimeSpan? duration) : EntityEventArgs
{
    public readonly EntityUid Target = target;
    public readonly EntProtoId StatusEffect = statusEffect;
    public readonly int Amount = amount;
    public readonly TimeSpan? Duration = duration;
    public bool Cancelled;
}
