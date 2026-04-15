using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Regeneration;

public sealed class CERegenerationStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CESharedDamageableSystem _damageable = default!;
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;

    private readonly EntProtoId _regenerationStatus = "CEStatusEffectRegeneration";
    private readonly TimeSpan _healInterval = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CERegenerationStatusEffectComponent, CEStatusEffectStackEffectEvent>(OnHeal);
    }

    private void OnHeal(Entity<CERegenerationStatusEffectComponent> ent, ref CEStatusEffectStackEffectEvent args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var effect) || effect.AppliedTo is null)
            return;

        _damageable.Heal(effect.AppliedTo.Value, ent.Comp.Amount * args.Stack, ent.Comp.Applier);
    }

    public void AddRegeneration(EntityUid target, EntityUid? source, int amount)
    {
        if (!_stack.TryAddStack(target, _regenerationStatus, out var effect, amount, _healInterval))
            return;

        if (!TryComp<CERegenerationStatusEffectComponent>(effect, out var regen))
            return;

        regen.Applier = source;
        Dirty(effect.Value, regen);
    }
}
