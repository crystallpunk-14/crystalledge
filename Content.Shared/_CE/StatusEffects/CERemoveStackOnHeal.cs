using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CERemoveStackOnHealComponent : Component
{
    /// <summary>
    /// How many heal units are required to remove 1 stack.
    /// </summary>
    [DataField]
    public int HealPerStack = 1;

    /// <summary>
    /// If false, cant remove last status effect stack
    /// </summary>
    [DataField]
    public bool CanRemoveStatusEffect = true;
}

public sealed class CERemoveStackOnHealSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CERemoveStackOnHealComponent, StatusEffectRelayedEvent<CEHealedEvent>>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<CERemoveStackOnHealComponent> ent, ref StatusEffectRelayedEvent<CEHealedEvent> args)
    {
        if (!TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            return;

        var stacksToRemove = args.Args.HealAmount / ent.Comp.HealPerStack;
        if (stacksToRemove <= 0)
            return;

        if (ent.Comp.CanRemoveStatusEffect)
        {
            _stack.TryRemoveStack((ent.Owner, stackComp), stacksToRemove);
        }
        else
        {
            var removedStacks = Math.Min(stackComp.Stacks - 1, stacksToRemove);
            _stack.TryRemoveStack((ent.Owner, stackComp), removedStacks);
        }
    }
}
