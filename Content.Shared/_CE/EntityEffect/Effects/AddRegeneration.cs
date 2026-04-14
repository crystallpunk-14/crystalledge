using Content.Shared._CE.Regeneration;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class AddRegeneration : CEEntityEffectBase<AddRegeneration>
{
    [DataField]
    public int Stacks = 1;
}

public sealed partial class CEAddRegenerationEffectSystem : CEEntityEffectSystem<AddRegeneration>
{
    [Dependency] private readonly CERegenerationStatusEffectSystem _regen = default!;

    protected override void Effect(ref CEEntityEffectEvent<AddRegeneration> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _regen.AddRegeneration(entity, args.Args.Source, args.Effect.Stacks);
    }
}
