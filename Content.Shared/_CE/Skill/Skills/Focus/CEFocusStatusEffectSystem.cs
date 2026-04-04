using Content.Shared._CE.Health;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Skill.Skills.Focus;

/// <summary>
/// Handles the Focus status effect: consumes one stack to grant a critical strike
/// when the attacker deals damage.
/// </summary>
public sealed partial class CEFocusStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEFocusStatusEffectComponent, StatusEffectRelayedEvent<CEIsCriticalDamageEvent>>(OnCritCheck);
    }

    private void OnCritCheck(Entity<CEFocusStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEIsCriticalDamageEvent> args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect))
            return;

        if (statusEffect.AppliedTo is null)
            return;

        var ev = args.Args;
        ev.IsCritical = true;
        args.Args = ev;

        if (TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            _stack.TryRemoveStack((ent.Owner, stackComp), 1);
    }
}
