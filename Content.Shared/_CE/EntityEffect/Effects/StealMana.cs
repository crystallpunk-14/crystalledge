using Content.Shared._CE.Mana.Core;

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
    [Dependency] private readonly CESharedMagicEnergySystem _mana = default!;

    protected override void Effect(ref CEEntityEffectEvent<StealMana> args)
    {
        if (ResolveEffectEntity(args.Args, CEEffectTarget.Target) is not { } sender)
            return;

        if (ResolveEffectEntity(args.Args, CEEffectTarget.User) is not { } receiver)
            return;

        _mana.TransferEnergy(sender, receiver, args.Effect.Amount, out _, out _);
    }
}
