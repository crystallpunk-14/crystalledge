using Content.Shared._CE.Water;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class WetEntity : CEEntityEffectBase<WetEntity>
{
    [DataField]
    public int Stacks = 1;

    [DataField]
    public int MaxStacks;
}

public sealed partial class CEWetEntityEffectSystem : CEEntityEffectSystem<WetEntity>
{
    [Dependency] private readonly CESharedWaterSystem _water = default!;

    protected override void Effect(ref CEEntityEffectEvent<WetEntity> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _water.WetEntity(entity, args.Effect.Stacks, args.Effect.MaxStacks > 0 ? args.Effect.MaxStacks : null);
    }
}
