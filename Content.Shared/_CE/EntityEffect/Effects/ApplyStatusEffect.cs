using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Applies a status effect, refreshing or setting its duration without adding stacks.
/// Use <see cref="ApplyStatusEffectStack"/> if you need stack accumulation.
/// </summary>
public sealed partial class ApplyStatusEffect : CEEntityEffectBase<ApplyStatusEffect>
{
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.FromSeconds(1f);
}

public sealed partial class CEApplyStatusEffectEffectSystem : CEEntityEffectSystem<ApplyStatusEffect>
{
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    protected override void Effect(ref CEEntityEffectEvent<ApplyStatusEffect> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        if (!_statusEffect.TrySetStatusEffectDuration(entity, args.Effect.StatusEffect, out var statusEnt, args.Effect.Duration))
            return;

        if (statusEnt != null && Exists(args.Args.Source))
        {
            var sourceComp = EnsureComp<CEStatusEffectSourceComponent>(statusEnt.Value);
            sourceComp.Source = args.Args.Source;
            Dirty(statusEnt.Value, sourceComp);
        }
    }
}
