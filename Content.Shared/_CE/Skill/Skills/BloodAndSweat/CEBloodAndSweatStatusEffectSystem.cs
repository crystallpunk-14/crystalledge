using Content.Shared._CE.Health;
using Content.Shared._CE.Stamina;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Skill.Skills.BloodAndSweat;

public sealed partial class CEBloodAndSweatStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CEStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBloodAndSweatStatusEffectComponent, StatusEffectRelayedEvent<CEDamageChangedEvent>>(OnDamageTaken);
    }

    private void OnDamageTaken(Entity<CEBloodAndSweatStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEDamageChangedEvent> args)
    {
        if (args.Args.DamageDelta <= 0)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect))
            return;

        if (statusEffect.AppliedTo is null)
            return;

        var amount = ent.Comp.StaminaRestore;

        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            amount *= stackComp.Stacks;

        _stamina.RestoreStamina(statusEffect.AppliedTo.Value, amount);
    }
}
