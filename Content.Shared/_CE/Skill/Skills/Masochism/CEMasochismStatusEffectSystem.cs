using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Skill.Skills.Masochism;

public sealed partial class CEMasochismStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CESharedMagicEnergySystem _magic = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMasochismStatusEffectComponent, StatusEffectRelayedEvent<DamageChangedEvent>>(OnDamageTaken);
    }

    private void OnDamageTaken(Entity<CEMasochismStatusEffectComponent> ent, ref StatusEffectRelayedEvent<DamageChangedEvent> args)
    {
        if (!args.Args.DamageIncreased)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect))
            return;

        if (statusEffect.AppliedTo is null)
            return;

        var count = ent.Comp.ManaRestore;

        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            count *= stackComp.Stack;

        _magic.ChangeEnergy(statusEffect.AppliedTo.Value, count, out _, out _);
    }
}
