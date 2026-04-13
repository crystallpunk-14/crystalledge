using Content.Shared._CE.DivineShield;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class AddDivineShield : CEEntityEffectBase<AddDivineShield>
{
    [DataField]
    public int Amount = 1;
}

public sealed partial class CEAddDivineShieldEffectSystem : CEEntityEffectSystem<AddDivineShield>
{
    [Dependency] private readonly CESharedDivineShieldSystem _divine = default!;

    protected override void Effect(ref CEEntityEffectEvent<AddDivineShield> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _divine.TryAddShield(entity, args.Args.Source, args.Effect.Amount);
    }
}
