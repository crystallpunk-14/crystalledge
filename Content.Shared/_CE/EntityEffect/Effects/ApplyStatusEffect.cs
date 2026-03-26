using Content.Shared._CE.StatusEffectStacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class ApplyStatusEffect : CEEntityEffectBase<ApplyStatusEffect>
{
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.FromSeconds(1f);

    [DataField]
    public int Stack = 1;
}

public sealed partial class CEApplyStatusEffectEffectSystem : CEEntityEffectSystem<ApplyStatusEffect>
{
    [Dependency] private readonly CEStatusEffectStackSystem _effectStack = default!;

    protected override void Effect(ref CEEntityEffectEvent<ApplyStatusEffect> args)
    {
        if (args.Args.Target is null)
            return;

        _effectStack.TryAddStack(args.Args.Target.Value, args.Effect.StatusEffect, args.Effect.Stack, args.Effect.Duration);
    }
}
