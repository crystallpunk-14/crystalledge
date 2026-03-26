using Content.Shared._CE.Mana.Core;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class StealMagicEnergy : CEEntityEffectBase<StealMagicEnergy>
{
    [DataField]
    public int EnergyCount = 1;

    [DataField]
    public bool Transfer = true;
}

public sealed partial class StealMagicEnergyEffectSystem : CEEntityEffectSystem<StealMagicEnergy>
{
    [Dependency] private readonly CESharedMagicEnergySystem _magicEnergy = default!;

    protected override void Effect(ref CEEntityEffectEvent<StealMagicEnergy> args)
    {
        var target = args.Args.Target;
        if (target is null || !_magicEnergy.HasEnergy(target.Value, args.Effect.EnergyCount))
            return;

        var targetEntity = target.Value;
        int energyCount = args.Effect.EnergyCount;

        if (args.Effect.Transfer)
        {
            _magicEnergy.ChangeEnergy(targetEntity, -energyCount, out _, out _);
            return;
        }

        if (args.Args.Used is not null)
        {
            _magicEnergy.TransferEnergy(targetEntity, args.Args.Used.Value, energyCount, out _, out _);
            return;
        }

        _magicEnergy.TransferEnergy(target.Value, args.Args.User, energyCount, out _, out _);
    }
}
