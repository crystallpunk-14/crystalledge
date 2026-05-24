using Content.Shared._CE.Stamina;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Restores stamina to the target entity.
/// </summary>
public sealed partial class RestoreStamina : CEEntityEffectBase<RestoreStamina>
{
    [DataField]
    public float Amount = 10f;
}

public sealed partial class CERestoreStaminaEffectSystem : CEEntityEffectSystem<RestoreStamina>
{
    [Dependency] private readonly CEStaminaSystem _stamina = default!;

    protected override void Effect(ref CEEntityEffectEvent<RestoreStamina> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _stamina.RestoreStamina(entity, args.Effect.Amount);
    }
}
