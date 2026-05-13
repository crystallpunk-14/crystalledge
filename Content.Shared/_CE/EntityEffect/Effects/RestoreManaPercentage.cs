using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class RestoreManaPercentage : CEEntityEffectBase<RestoreManaPercentage>
{
    /// <summary>
    /// Fraction of <see cref="CEMagicEnergyContainerComponent.MaxEnergy"/> to restore. 1.0 = 100%.
    /// </summary>
    [DataField]
    public float Percentage = 1f;

    [DataField]
    public bool ApplyModifiers = true;
}

public sealed partial class CERestoreManaPercentageEffectSystem : CEEntityEffectSystem<RestoreManaPercentage>
{
    [Dependency] private readonly CESharedMagicEnergySystem _mana = default!;

    protected override void Effect(ref CEEntityEffectEvent<RestoreManaPercentage> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        if (!TryComp<CEMagicEnergyContainerComponent>(entity, out var container))
            return;

        if (container.MaxEnergy <= 0)
            return;

        var amount = (int)(container.MaxEnergy * args.Effect.Percentage);
        if (amount <= 0)
            return;

        _mana.Restore((entity, (CEMagicEnergyContainerComponent?) container), amount, args.Args.Source, args.Effect.ApplyModifiers);
    }
}
