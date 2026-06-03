using Content.Shared._CE.ZLevels.Core.EntitySystems;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class AddZVelocity : CEEntityEffectBase<AddZVelocity>
{
    [DataField(required: true)]
    public float Speed = 0f;
}

public sealed partial class CEAddZVelocitySystem : CEEntityEffectSystem<AddZVelocity>
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevel = default!;

    protected override void Effect(ref CEEntityEffectEvent<AddZVelocity> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _zLevel.AddZVelocity(entity, args.Effect.Speed);
    }
}
