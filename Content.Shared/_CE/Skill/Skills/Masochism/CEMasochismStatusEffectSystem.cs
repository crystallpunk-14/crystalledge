using Content.Shared._CE.Health;
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

        SubscribeLocalEvent<CEMasochismStatusEffectComponent, StatusEffectRelayedEvent<CEDamageChangedEvent>>(OnDamageTaken);
    }

    private void OnDamageTaken(Entity<CEMasochismStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEDamageChangedEvent> args)
    {
        if (args.Args.DamageDelta <= 0)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect))
            return;

        if (statusEffect.AppliedTo is null)
            return;

        var count = ent.Comp.ManaRestore;

        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            count *= stackComp.Stacks;

        _magic.Restore(statusEffect.AppliedTo.Value, count, statusEffect.AppliedTo.Value);
    }
}
