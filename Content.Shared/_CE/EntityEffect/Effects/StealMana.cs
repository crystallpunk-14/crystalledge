using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Transfers mana from the target to the user.
/// </summary>
public sealed partial class StealMana : CEEntityEffectBase<StealMana>
{
    [DataField]
    public int Amount = 1;
}

public sealed partial class CEStealManaEffectSystem : CEEntityEffectSystem<StealMana>
{
    [Dependency] private CESharedMagicEnergySystem _mana = default!;

    [Dependency] private EntityQuery<CEMagicEnergyContainerComponent> _energyQuery = default!;

    protected override void Effect(ref CEEntityEffectEvent<StealMana> args)
    {
        if (ResolveEffectEntity(args.Args, CEEffectTarget.Target) is not { } target)
            return;

        if (ResolveEffectEntity(args.Args, CEEffectTarget.User) is not { } user)
            return;

        if (!_energyQuery.TryComp(target, out var targetEnergy))
            return;

        if (!_energyQuery.TryComp(user, out var userEnergy))
            return;

        _mana.TransferEnergy((target, targetEnergy), (user, userEnergy), args.Effect.Amount, out _, out _);
    }
}
