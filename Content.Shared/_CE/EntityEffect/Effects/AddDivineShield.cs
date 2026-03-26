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
        if (args.Args.Target is null)
            return;

        _divine.TryAddShield(args.Args.Target.Value, args.Effect.Amount);
    }
}
