using Content.Shared._CE.Frost;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class FreezeEntity : CEEntityEffectBase<FreezeEntity>
{
    [DataField]
    public int Stacks = 1;

    [DataField]
    public int MaxStacks;
}

public sealed partial class CEFreezeEntityEffectSystem : CEEntityEffectSystem<FreezeEntity>
{
    [Dependency] private readonly CEFrostSystem _frost = default!;

    protected override void Effect(ref CEEntityEffectEvent<FreezeEntity> args)
    {
        if (args.Args.Target is not { } target)
            return;

        _frost.FreezeEntity(target, args.Effect.Stacks, args.Effect.MaxStacks > 0 ? args.Effect.MaxStacks : null);
    }
}
