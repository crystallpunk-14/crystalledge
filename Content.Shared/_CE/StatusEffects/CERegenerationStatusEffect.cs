using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CERegenerationStatusEffectComponent : Component
{
    [DataField]
    public int Amount = 1;

    /// <summary>
    /// Should healing be scaled based on the number of stacks of this status effect?
    /// </summary>
    [DataField]
    public bool ScaleWithStacks = true;
}

public sealed partial class CERegenerationStatusEffectSystem : EntitySystem
{
    [Dependency] private CESharedDamageableSystem _damageable = default!;
    [Dependency] private CEStatusEffectStackSystem _stack = default!;

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
