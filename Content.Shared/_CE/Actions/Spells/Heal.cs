using Content.Shared._CE.Health;

namespace Content.Shared._CE.Actions.Spells;

public sealed partial class Heal: CESpellEffect
{
    [DataField]
    public int Amount = 1;

    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        if (args.Target is null)
            return;

        var health = entManager.System<CESharedHealthSystem>();
        health.Heal(args.Target.Value, Amount, args.User);
    }
}
