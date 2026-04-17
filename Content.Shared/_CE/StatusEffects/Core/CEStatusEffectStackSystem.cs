using Content.Shared.Alert;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.StatusEffectStacks;

public sealed class CEStatusEffectStackSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStatusEffectStackComponent, CEStatusEffectEndingAttemptEvent>(OnBeforeEnded);
        SubscribeLocalEvent<CEStatusEffectNeutralizationComponent, StatusEffectRelayedEvent<CEStackAddAttemptEvent>>(OnNeutralize);
    }

    /// <summary>
    /// Handles a burn cycle tick. Applies the effect, adjusts stacks, and extends the timer.
    /// On the final tick (stacks drop to 0), applies one last effect and lets the status end.
    /// </summary>
    private void OnBeforeEnded(Entity<CEStatusEffectStackComponent> ent, ref CEStatusEffectEndingAttemptEvent args)
    {
        var delta = ent.Comp.StackDelta;
        var newStack = ent.Comp.Stacks + delta;

        // Always apply the effect on a cycle tick (server-only).
        if (!_net.IsClient)
        {
            var ev = new CEStatusEffectStackEffectEvent(ent.Comp.Stacks);
            RaiseLocalEvent(ent, ref ev);
        }

        // Final tick — stacks depleted, let the effect end.
        if (newStack <= 0)
            return;

        // More stacks remain — cancel ending and schedule the next cycle.
        args.Cancelled = true;

        if (_net.IsClient)
            return;

        var proto = MetaData(ent).EntityPrototype;
        if (proto is null)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect) || statusEffect.AppliedTo is null)
            return;

        var duration = ent.Comp.BaseDuration;
        if (duration is null)
            return;

        _statusEffect.TryAddTime(statusEffect.AppliedTo.Value, proto, duration.Value);

        if (delta < 0)
            TryRemoveStack(statusEffect.AppliedTo.Value, proto, -delta);
        else if (delta > 0)
            TryAddStack(statusEffect.AppliedTo.Value, proto, out _, delta);
    }

    /// <summary>
    /// Adds the specified number of stacks of a status effect to the entity.
    /// This either adds the status effect if it does not exist, or edits the existing status effect.
    /// </summary>
    /// <param name="target">Target entity with StatusEffectContainer</param>
    /// <param name="statusEffect">Type of status effect.</param>
    /// <param name="stack">Optional, default 1. Number of stacks. Cannot be a negative number.</param>
    /// <param name="duration">Optional: status effect duration. If specified, the new status effect will have the specified duration, and the duration of the existing status effect will be edited.</param>
    /// <param name="resetTimer">If true and the effect already exists, resets the cycle timer to the full duration instead of letting it continue from the current point.</param>
    /// <returns>True if the status effect was successfully added or its stack count was increased. False if for some reason this could not be done.</returns>
    public bool TryAddStack(EntityUid target, EntProtoId statusEffect, out EntityUid? effectEntity, int stack = 1, TimeSpan? duration = null, bool resetTimer = false)
    {
        effectEntity = null;

        if (stack <= 0)
            return false;

        // Raise attempt event so neutralization and other handlers can modify or cancel stacks.
        var attemptEv = new CEStackAddAttemptEvent(statusEffect, stack);
        RaiseLocalEvent(target, attemptEv);

        if (attemptEv.Cancelled || attemptEv.Stacks <= 0)
            return false;

        stack = attemptEv.Stacks;

        if (!_statusEffect.TryGetStatusEffect(target, statusEffect, out var statusEnt))
        {
            if (!_statusEffect.TrySetStatusEffectDuration(target, statusEffect, out statusEnt, duration))
                return false;

            effectEntity = statusEnt;
            var stackComp = EnsureComp<CEStatusEffectStackComponent>(statusEnt.Value);

            // Use the explicit duration, or fall back to the prototype-defined BaseDuration.
            var effectiveDuration = duration ?? stackComp.BaseDuration;
            if (effectiveDuration != null)
            {
                stackComp.BaseDuration = effectiveDuration;

                // If we used the prototype default, set the actual timer now.
                if (duration == null)
                    _statusEffect.TrySetStatusEffectDuration(target, statusEffect, effectiveDuration);
            }

            SetStack(target, (statusEnt.Value, stackComp), stack);
            return true;
        }
        else
        {
            effectEntity = statusEnt;
            var stackComp = EnsureComp<CEStatusEffectStackComponent>(statusEnt.Value);
            SetStack(target, (statusEnt.Value, stackComp), stackComp.Stacks + stack);

            if (duration != null)
            {
                stackComp.BaseDuration = duration;
                Dirty(statusEnt.Value, stackComp);
            }

            var shouldReset = resetTimer || stackComp.ResetTimerOnStack;
            if (shouldReset)
            {
                var effectiveDuration = duration ?? stackComp.BaseDuration;
                if (effectiveDuration != null)
                    _statusEffect.TrySetStatusEffectDuration(target, statusEffect, effectiveDuration);
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

        if (stackComp.Stacks <= stack)
        {
            _statusEffect.TryRemoveStatusEffect(target, statusEffect);
            return true;
        }

        SetStack(target, (statusEnt.Value, stackComp), stackComp.Stacks - stack);
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

        if (effect.Comp.Stacks <= stack)
        {
            _statusEffect.TryRemoveStatusEffect(statusEffect.AppliedTo.Value, proto);
            return true;
        }

        SetStack(statusEffect.AppliedTo.Value, (effect, effect.Comp), effect.Comp.Stacks - stack);
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

        return stackComp.Stacks;
    }

    public int GetStack(EntityUid effect)
    {
        if (!TryComp<CEStatusEffectStackComponent>(effect, out var stackComp))
            return 0;

        return stackComp.Stacks;
    }

    private void OnNeutralize(EntityUid uid, CEStatusEffectNeutralizationComponent comp,
        ref StatusEffectRelayedEvent<CEStackAddAttemptEvent> args)
    {
        if (args.Args.Cancelled || args.Args.Stacks <= 0)
            return;

        if (!comp.Neutralizes.Contains(args.Args.StatusEffect))
            return;

        var myStacks = GetStack(uid);
        if (myStacks <= 0)
            return;

        var neutralized = Math.Min(myStacks, args.Args.Stacks);
        TryRemoveStack(uid, neutralized);
        args.Args.Stacks -= neutralized;

        if (comp.Vfx != null && TryComp<StatusEffectComponent>(uid, out var statusComp) && statusComp.AppliedTo is { } target)
        {
            var coords = Transform(target).Coordinates;
            Spawn(comp.Vfx, coords);

            if (comp.Sound != null)
                _audio.PlayPvs(comp.Sound, coords);
        }

        if (args.Args.Stacks <= 0)
            args.Args.Cancel();
    }

    private void SetStack(EntityUid target, Entity<CEStatusEffectStackComponent> ent, int newStack)
    {
        if (ent.Comp.Stacks == newStack)
            return;

        var oldStack = ent.Comp.Stacks;

        ent.Comp.Stacks = newStack;
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

        var appearanceState = CEStatusEffectStackPowerVisuals.Low;
        if (newStack >= ent.Comp.MediumAppearance)
            appearanceState = CEStatusEffectStackPowerVisuals.Medium;
        if (newStack >= ent.Comp.HighAppearance)
            appearanceState = CEStatusEffectStackPowerVisuals.High;

        _appearance.SetData(ent, CEStatusEffectStackVisuals.Level, appearanceState);
    }

    /// <summary>
    /// Sets the StackDelta on a specific status effect applied to a target entity.
    /// </summary>
    public void SetStackDelta(EntityUid target, EntProtoId statusEffect, int delta)
    {
        if (!_statusEffect.TryGetStatusEffect(target, statusEffect, out var statusEnt))
            return;

        if (!TryComp<CEStatusEffectStackComponent>(statusEnt.Value, out var stackComp))
            return;

        stackComp.StackDelta = delta;
        Dirty(statusEnt.Value, stackComp);
    }
}

/// <summary>
/// Calls on effect entity, when a status effect stack is edited
/// </summary>
[ByRefEvent]
public readonly record struct CEStatusEffectStackEditedEvent(EntityUid Target, int oldStack, int newStack);

/// <summary>
/// Calls on effect entity, when a status effect stacks effect should happens
/// </summary>
[ByRefEvent]
public readonly record struct CEStatusEffectStackEffectEvent(int Stack);

[NetSerializable, Serializable]
public enum CEStatusEffectStackVisuals
{
    Level,
}

[NetSerializable, Serializable]
public enum CEStatusEffectStackPowerVisuals
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Raised on the target entity before stacks of a status effect are added.
/// Relayed to all active status effects via <see cref="StatusEffectRelayedEvent{T}"/>.
/// </summary>
public sealed class CEStackAddAttemptEvent(EntProtoId statusEffect, int stacks) : CancellableEntityEventArgs
{
    public EntProtoId StatusEffect = statusEffect;
    public int Stacks = stacks;
}
