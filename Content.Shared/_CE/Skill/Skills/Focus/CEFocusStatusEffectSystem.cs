using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.Health;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.Skill.Skills.Focus;

/// <summary>
/// Handles the Focus status effect: grants guaranteed critical strikes
/// for the entire duration of the effect.
/// </summary>
public sealed partial class CEFocusStatusEffectSystem : EntitySystem
{
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

        if (args.Args.Weapon is null)
            return;

        if (!TryComp<CEWeaponComponent>(args.Args.Weapon, out var weapon)) //block criting for spells
            return;

        var ev = args.Args;
        ev.IsCritical = true;
        args.Args = ev;
    }
}
