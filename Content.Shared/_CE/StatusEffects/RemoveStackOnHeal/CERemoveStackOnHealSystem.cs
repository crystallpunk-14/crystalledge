using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects.RemoveStackOnHeal;

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

        if (ent.Comp.CanRemoveStatusEffect)
        {
            _stack.TryRemoveStack((ent.Owner, stackComp), ent.Comp.Amount);
        }
        else
        {
            var removedStacks = Math.Min(stackComp.Stacks - 1, ent.Comp.Amount);
            _stack.TryRemoveStack((ent.Owner, stackComp), removedStacks);
        }
    }
}
