using Content.Shared._CE.TempShield;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.Skill.Skills.ThickSkin;

public sealed partial class CEThickSkinStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEThickSkinStatusEffectComponent, StatusEffectRelayedEvent<CECalculateTempShieldStacksEvent>>(OnCalculateStacks);
    }

    private void OnCalculateStacks(Entity<CEThickSkinStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CECalculateTempShieldStacksEvent> args)
    {
        args.Args.Stacks = (int)(args.Args.Stacks * ent.Comp.TempShieldMultiplier);
    }
}
