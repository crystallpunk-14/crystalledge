using Content.Shared._CE.Mana.Core;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class RestoreMana : CEEntityEffectBase<RestoreMana>
{
    [DataField]
    public int Amount = 1;
}

public sealed partial class CERestoreManaEffectSystem : CEEntityEffectSystem<RestoreMana>
{
    [Dependency] private readonly CESharedMagicEnergySystem _mana = default!;

    protected override void Effect(ref CEEntityEffectEvent<RestoreMana> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _mana.ChangeEnergy(entity, args.Effect.Amount, out _, out _);
    }
}
