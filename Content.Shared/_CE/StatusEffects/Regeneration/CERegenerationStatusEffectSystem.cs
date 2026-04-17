using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Regeneration;

public sealed class CERegenerationStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CESharedDamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CERegenerationStatusEffectComponent, CEStatusEffectStackEffectEvent>(OnHeal);
    }

    private void OnHeal(Entity<CERegenerationStatusEffectComponent> ent, ref CEStatusEffectStackEffectEvent args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var effect) || effect.AppliedTo is null)
            return;

        TryComp<CEStatusEffectSourceComponent>(ent, out var sourceComp);
        var source = sourceComp?.Source is { } s && Exists(s) ? s : (EntityUid?) null;
        _damageable.Heal(effect.AppliedTo.Value, ent.Comp.Amount * args.Stack, source);
    }
}
