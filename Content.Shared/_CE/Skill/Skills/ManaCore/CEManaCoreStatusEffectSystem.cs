using Content.Shared._CE.Health;
using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.Skill.Skills.ManaCore;

public sealed partial class CEManaCoreStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEManaCoreStatusEffectComponent, StatusEffectRelayedEvent<CEGetManaRestoringAmountEvent>>(OnRestore);
    }

    private void OnRestore(Entity<CEManaCoreStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEGetManaRestoringAmountEvent> args)
    {
        var amount = args.Args.RestoreAmount;

        amount = (int)(amount * ent.Comp.ManaRestoreMultiplier);

        args.Args.RestoreAmount = amount;
    }
}
