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
        if (args.Args.Target is null)
            return;

        _mana.ChangeEnergy(args.Args.Target.Value, args.Effect.Amount, out _, out _);
    }
}
