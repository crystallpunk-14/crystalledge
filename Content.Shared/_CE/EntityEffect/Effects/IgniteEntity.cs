using Content.Shared._CE.Fire;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class IgniteEntity : CEEntityEffectBase<IgniteEntity>
{
    [DataField]
    public int Stacks = 1;

    [DataField]
    public int MaxStacks;
}

public sealed partial class CEIgniteEntityEffectSystem : CEEntityEffectSystem<IgniteEntity>
{
    [Dependency] private readonly CEFireSystem _fire = default!;

    protected override void Effect(ref CEEntityEffectEvent<IgniteEntity> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _fire.IgniteEntity(entity, args.Args.User, args.Effect.Stacks, args.Effect.MaxStacks > 0 ? args.Effect.MaxStacks : null);
    }
}
