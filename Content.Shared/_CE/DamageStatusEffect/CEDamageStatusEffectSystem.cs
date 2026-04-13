using Content.Shared._CE.DamageStatusEffect.Components;
using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew.Components;

public sealed partial class CEDamageStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CESharedDamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageStatusEffectComponent, CEStatusEffectStackEffectEvent>(OnEffect);
    }

    private void OnEffect(Entity<CEDamageStatusEffectComponent> ent, ref CEStatusEffectStackEffectEvent args)
    {
        Damage(ent, args.Stack);
    }

    private void Damage(Entity<CEDamageStatusEffectComponent> ent, int stack)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var effect) || effect.AppliedTo is null)
            return;

        _damageable.TakeDamage(effect.AppliedTo.Value, ent.Comp.Damage * stack, interruptDoAfters: ent.Comp.InterruptDoAfters);
    }
}
