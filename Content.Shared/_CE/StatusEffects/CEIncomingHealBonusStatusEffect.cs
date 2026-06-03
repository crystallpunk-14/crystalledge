using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

/// <summary>
/// Status effect that increases incoming healing on the entity it's applied to.
/// Scales with stacks via <see cref="CEStatusEffectStackComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEIncomingHealBonusStatusEffectComponent : Component
{
    [DataField]
    public int BonusPerStack = 1;
}

public sealed partial class CEIncomingHealBonusStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEIncomingHealBonusStatusEffectComponent, StatusEffectRelayedEvent<CEGetIncomingHealEvent>>(OnIncomingHeal);
    }

    private void OnIncomingHeal(Entity<CEIncomingHealBonusStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEGetIncomingHealEvent> args)
    {
        var bonus = ent.Comp.BonusPerStack;
        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            bonus *= stackComp.Stacks;

        args.Args.HealAmount += bonus;
    }
}
