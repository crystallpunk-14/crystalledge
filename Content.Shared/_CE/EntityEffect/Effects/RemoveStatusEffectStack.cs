using Content.Shared._CE.StatusEffects.Core;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Removes a number of stacks from a status effect on the target entity.
/// If the effect uses stacks, reduces them by <see cref="Amount"/>.
/// If no stacks remain, or the effect has no stack component, the effect is fully removed.
/// </summary>
public sealed partial class RemoveStatusEffectStack : CEEntityEffectBase<RemoveStatusEffectStack>
{
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    [DataField]
    public int Amount = 1;
}

public sealed partial class CERemoveStatusEffectStackSystem : CEEntityEffectSystem<RemoveStatusEffectStack>
{
    [Dependency] private CEStatusEffectStackSystem _effectStack = default!;
    [Dependency] private StatusEffectsSystem _statusEffect = default!;

    protected override void Effect(ref CEEntityEffectEvent<RemoveStatusEffectStack> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        // Try to remove stacks first; if the effect has no stack component, fully remove it.
        if (!_effectStack.TryRemoveStack(entity, args.Effect.StatusEffect, (int)(args.Effect.Amount * args.Args.Power)))
            _statusEffect.TryRemoveStatusEffect(entity, args.Effect.StatusEffect);
    }
}
