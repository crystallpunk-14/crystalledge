using Content.Shared._CE.DivineShield;

namespace Content.Shared._CE.Actions.Spells;

public sealed partial class AddDivineShield : CESpellEffect
{
    [DataField]
    public int Amount = 1;

    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        if (args.Target is null)
            return;

        var divine = entManager.System<CEDivineShieldSystem>();
        divine.TryAddShield(args.Target.Value, Amount);
    }
}
