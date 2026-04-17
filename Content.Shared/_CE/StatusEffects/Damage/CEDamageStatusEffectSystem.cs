using Content.Shared._CE.DamageStatusEffect.Components;
using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.DamageStatusEffect;

public sealed partial class CEDamageStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CESharedDamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageStatusEffectComponent, CEStatusEffectStackEffectEvent>(OnDamage);
    }

    private void OnDamage(Entity<CEDamageStatusEffectComponent> ent, ref CEStatusEffectStackEffectEvent args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var effect) || effect.AppliedTo is null)
            return;

        _damageable.TakeDamage(effect.AppliedTo.Value, ent.Comp.Damage * args.Stack, ignoreArmor: ent.Comp.IgnoreArmor, interruptDoAfters: ent.Comp.InterruptDoAfters);
    }
}
