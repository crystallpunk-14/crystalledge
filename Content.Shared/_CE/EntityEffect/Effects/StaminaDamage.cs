using Content.Shared.Damage.Systems;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class StaminaDamage : CEEntityEffectBase<StaminaDamage>
{
    [DataField]
    public float Damage = 10f;
}

public sealed partial class CEStaminaDamageEffectSystem : CEEntityEffectSystem<StaminaDamage>
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    protected override void Effect(ref CEEntityEffectEvent<StaminaDamage> args)
    {
        if (args.Args.Target is null)
            return;

        _stamina.TakeStaminaDamage(args.Args.Target.Value, args.Effect.Damage, null, args.Args.User, args.Args.Used);
    }
}
