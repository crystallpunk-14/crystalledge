using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

/// <summary>
/// Deals damage to the entity each time a status effect is applied or the number of stacks of that status effect is updated.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEDamageStatusEffectComponent : Component
{
    [DataField(required: true)]
    public CEDamageSpecifier Damage = new();

    /// <summary>
    /// Should damage be scaled based on the number of stacks of this status effect?
    /// </summary>
    [DataField]
    public bool ScaleWithStacks = true;

    [DataField]
    public bool InterruptDoAfters = true;

    [DataField]
    public bool IgnoreArmor = false;
}

public sealed partial class CEDamageStatusEffectSystem : EntitySystem
{
    [Dependency] private CESharedDamageableSystem _damageable = default!;
    [Dependency] private CEStatusEffectStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageStatusEffectComponent, CEStatusEffectStackEffectEvent>(OnDamage);
    }

    private void OnDamage(Entity<CEDamageStatusEffectComponent> ent, ref CEStatusEffectStackEffectEvent args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var effect) || effect.AppliedTo is null)
            return;

        _damageable.TakeDamage(effect.AppliedTo.Value, ent.Comp.Damage * args.Stack, _stack.GetSource(ent.Owner), ignoreArmor: ent.Comp.IgnoreArmor, interruptDoAfters: ent.Comp.InterruptDoAfters);
    }
}
