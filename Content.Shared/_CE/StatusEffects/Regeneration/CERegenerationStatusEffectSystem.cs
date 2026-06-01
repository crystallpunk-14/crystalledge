using Content.Shared._CE.Health;
using Content.Shared._CE.Regeneration;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.StatusEffects.Regeneration;

public sealed class CERegenerationStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CESharedDamageableSystem _damageable = default!;
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CERegenerationStatusEffectComponent, CEStatusEffectStackEffectEvent>(OnHeal);
    }

    private void OnHeal(Entity<CERegenerationStatusEffectComponent> ent, ref CEStatusEffectStackEffectEvent args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var effect) || effect.AppliedTo is null)
            return;

        _damageable.Heal(effect.AppliedTo.Value, ent.Comp.Amount * args.Stack, _stack.GetSource(ent.Owner));
    }
}
