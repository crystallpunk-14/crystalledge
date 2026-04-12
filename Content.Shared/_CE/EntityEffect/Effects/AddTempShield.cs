using Content.Shared._CE.Health.Prototypes;
using Content.Shared._CE.TempShield;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class AddTempShield : CEEntityEffectBase<AddTempShield>
{
    [DataField]
    public int Amount = 1;

    [DataField]
    public ProtoId<CEDamageTypePrototype> DamageType = "Physical";
}

public sealed partial class CEAddTempShieldEffectSystem : CEEntityEffectSystem<AddTempShield>
{
    [Dependency] private readonly CETempShieldSystem _tempShield = default!;

    protected override void Effect(ref CEEntityEffectEvent<AddTempShield> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _tempShield.TryAddTempShield(entity, args.Effect.DamageType, args.Effect.Amount);
    }
}
