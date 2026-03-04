using Content.Shared.Alert;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffectStacks;

public sealed class CEStatusEffectStackSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStatusEffectStackComponent, CEStatusEffectEndingAttemptEvent>(OnBeforeEnded);
    }

    private void OnBeforeEnded(Entity<CEStatusEffectStackComponent> ent, ref CEStatusEffectEndingAttemptEvent args)
    {
        if (ent.Comp.Stack <= 1)
            return;

        var proto = MetaData(ent).EntityPrototype;
        if (proto is null)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect) || statusEffect.AppliedTo is null)
            return;

        // Use the stored base duration instead of calculating from time difference
        var duration = ent.Comp.BaseDuration;
        if (duration is null)
            return;

        _statusEffect.TryAddTime(statusEffect.AppliedTo.Value, proto, duration.Value);
        args.Cancelled = true;
        TryRemoveStack(statusEffect.AppliedTo.Value, proto, 1);
    }

    /// <summary>
    /// Adds the specified number of stacks of a status effect to the entity.
    /// This either adds the status effect if it does not exist, or edits the existing status effect.
    /// </summary>
    /// <param name="target">Target entity with StatusEffectContainer</param>
    /// <param name="statusEffect">Type of status effect.</param>
    /// <param name="stack">Optional, default 1. Number of stacks. Cannot be a negative number.</param>
    /// <param name="duration">Optional: status effect duration. If specified, the new status effect will have the specified duration, and the duration of the existing status effect will be edited.</param>
    /// <returns>True if the status effect was successfully added or its stack count was increased. False if for some reason this could not be done.</returns>
    public bool TryAddStack(EntityUid target, EntProtoId statusEffect, int stack = 1, TimeSpan? duration = null)
    {
        if (stack <= 0)
            return false;

        if (!_statusEffect.TryGetStatusEffect(target, statusEffect, out var statusEnt))
        {
            if (!_statusEffect.TrySetStatusEffectDuration(target, statusEffect, out statusEnt, duration))
                return false;

            var stackComp = EnsureComp<CEStatusEffectStackComponent>(statusEnt.Value);
            stackComp.BaseDuration = duration;
            SetStack(target, (statusEnt.Value, stackComp), stack);
            return true;
        }
        else
        {
            var stackComp = EnsureComp<CEStatusEffectStackComponent>(statusEnt.Value);
            SetStack(target, (statusEnt.Value, stackComp), stackComp.Stack + stack);
            if (duration != null)
            {
                stackComp.BaseDuration = duration;
                Dirty(statusEnt.Value, stackComp);
            }
            return true;
        }
    }

    /// <summary>
    /// Attempt to remove status effect stacks from an entity.
    /// This either edits the number of status effect stacks or removes the status effect.
    /// </summary>
    /// <param name="target">Target entity with StatusEffectContainer</param>
    /// <param name="statusEffect">Type of status effect.</param>
    /// <param name="stack">Optional, default 1. Number of stacks. Cannot be a negative number.</param>
    /// <returns>True if the status effect was successfully removed or its number of stacks was reduced. False if for some reason this could not be done.</returns>
    public bool TryRemoveStack(EntityUid target, EntProtoId statusEffect, int stack = 1)
    {
        if (stack <= 0)
            return false;

        if (!_statusEffect.TryGetStatusEffect(target, statusEffect, out var statusEnt))
            return false;

        if (!TryComp<CEStatusEffectStackComponent>(statusEnt.Value, out var stackComp))
            return false;

        if (stackComp.Stack <= stack)
        {
            _statusEffect.TryRemoveStatusEffect(target, statusEffect);
            return true;
        }

        SetStack(target, (statusEnt.Value, stackComp), stackComp.Stack - stack);
        return true;
    }

    /// <summary>
    /// Attempt to remove status effect stacks from an entity.
    /// This either edits the number of status effect stacks or removes the status effect.
    /// </summary>
    /// <param name="effect">Status effect entity</param>
    /// <param name="stack">Optional, default 1. Number of stacks. Cannot be a negative number.</param>
    /// <returns>True if the status effect was successfully removed or its number of stacks was reduced. False if for some reason this could not be done.</returns>
    public bool TryRemoveStack(Entity<CEStatusEffectStackComponent?> effect, int stack = 1)
    {
        if (stack <= 0)
            return false;

        if (!Resolve(effect, ref effect.Comp, false))
            return false;

        if (!TryComp<StatusEffectComponent>(effect, out var statusEffect) || statusEffect.AppliedTo is null)
            return false;

        var proto = MetaData(effect).EntityPrototype;

        if (proto is null)
            return false;

        if (effect.Comp.Stack <= stack)
        {
            _statusEffect.TryRemoveStatusEffect(statusEffect.AppliedTo.Value, proto);
            return true;
        }

        SetStack(statusEffect.AppliedTo.Value, (effect, effect.Comp), effect.Comp.Stack - stack);
        return true;
    }

    /// <summary>
    /// Returns the number of stacks of a specific status effect from an entity.
    /// Returns 0 if the status effect does not exist or if there is any other problem.
    /// </summary>
    /// <param name="target">Target entity with StatusEffectContainer</param>
    /// <param name="statusEffect">Type of status effect.</param>
    public int GetStack(EntityUid target, EntProtoId statusEffect)
    {
        if (!_statusEffect.TryGetStatusEffect(target, statusEffect, out var statusEnt))
            return 0;

        if (!TryComp<CEStatusEffectStackComponent>(statusEnt.Value, out var stackComp))
            return 0;

        return stackComp.Stack;
    }

    private void SetStack(EntityUid target, Entity<CEStatusEffectStackComponent> ent, int newStack)
    {
        if (ent.Comp.Stack == newStack)
            return;

        var oldStack = ent.Comp.Stack;

        ent.Comp.Stack = newStack;
        Dirty(ent);

        var ev = new CEStatusEffectStackEditedEvent(target, oldStack, newStack);
        RaiseLocalEvent(ent.Owner, ref ev);

        if (TryComp<StatusEffectAlertComponent>(ent, out var alertComp))
        {
            TimeSpan? cooldown = null;
            if (alertComp.ShowDuration && TryComp<StatusEffectComponent>(ent, out var effectComp))
                cooldown = effectComp.EndEffectTime;
            _alerts.UpdateAlert(target, alertComp.Alert, cooldown: cooldown);
        }
    }
}

/// <summary>
/// Calls on effect entity, when a status effect stack is edited
/// </summary>
[ByRefEvent]
public readonly record struct CEStatusEffectStackEditedEvent(EntityUid Target, int oldStack, int newStack);
