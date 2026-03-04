using Content.Shared._CE.StatusEffectStacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Actions.Spells;

public sealed partial class CESpellApplyStatusEffect : CESpellEffect
{
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.FromSeconds(1f);

    [DataField]
    public int Stack = 1;

    public override void Effect(EntityManager entManager, CESpellEffectBaseArgs args)
    {
        if (args.Target is null)
            return;

        var effectSys = entManager.System<CEStatusEffectStackSystem>();
        effectSys.TryAddStack(args.Target.Value, StatusEffect, Stack, Duration);
    }
}
