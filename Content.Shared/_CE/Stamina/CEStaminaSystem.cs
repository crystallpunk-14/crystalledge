using Content.Shared.Inventory;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Stamina;

public sealed class CEStaminaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStaminaComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<CEStaminaComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRefreshMoveSpeed(EntityUid uid, CEStaminaComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.Exhausted)
            args.ModifySpeed(comp.ExhaustedSpeedModifier, comp.ExhaustedSpeedModifier);
    }

    private void OnRejuvenate(Entity<CEStaminaComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Stamina = ent.Comp.MaxStamina;
        ent.Comp.RegenStartTime = TimeSpan.Zero;

        if (ent.Comp.Exhausted)
        {
            ent.Comp.Exhausted = false;
            _movement.RefreshMovementSpeedModifiers(ent);
        }

        Dirty(ent);
    }

    /// <summary>
    /// Checks if regenerating entities have reached max stamina and snapshots the final state.
    /// Dirty is only called when stamina actually reaches max, not every frame.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CEStaminaComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            // Skip if already at max.
            if (comp.Stamina >= comp.MaxStamina)
                continue;

            // Skip if regen hasn't started yet.
            if (curTime < comp.RegenStartTime)
                continue;

            var computed = GetComputedStamina(comp, curTime);

            // Only dirty+snapshot when stamina reaches max.
            if (computed < comp.MaxStamina)
                continue;

            comp.Stamina = comp.MaxStamina;
            Dirty(uid, comp);

            if (comp.Exhausted)
            {
                comp.Exhausted = false;
                _movement.RefreshMovementSpeedModifiers(uid);
                Dirty(uid, comp);
            }
        }
    }

    /// <summary>
    /// Tries to spend stamina. Returns true if stamina was available (>0 and not exhausted),
    /// false if the entity is exhausted or has no stamina.
    /// </summary>
    public bool TryTakeDamage(Entity<CEStaminaComponent?> ent, float amount)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return true; //Dont block using item for entities without stamina component

        if (ent.Comp.Exhausted)
        {
            TryPopupNotEnough(ent, ent.Comp);
            return false;
        }

        var current = GetStamina(ent);

        if (current <= 0)
        {
            TryPopupNotEnough(ent, ent.Comp);
            return false;
        }

        // Snapshot current computed stamina minus damage.
        var newStamina = Math.Max(current - amount, 0f);
        ent.Comp.Stamina = newStamina;
        ent.Comp.RegenStartTime = _timing.CurTime + ent.Comp.RegenCooldown;

        if (newStamina <= 0)
        {
            ent.Comp.Exhausted = true;
            _movement.RefreshMovementSpeedModifiers(ent);
        }

        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Restores stamina by the given amount, clamped to max.
    /// If the entity was exhausted and stamina reaches max, clears exhaustion.
    /// </summary>
    public void RestoreStamina(Entity<CEStaminaComponent?> ent, float amount)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var current = GetStamina(ent);
        var newStamina = Math.Min(current + amount, ent.Comp.MaxStamina);
        ent.Comp.Stamina = newStamina;
        ent.Comp.RegenStartTime = _timing.CurTime;

        if (ent.Comp.Exhausted && newStamina >= ent.Comp.MaxStamina)
        {
            ent.Comp.Exhausted = false;
            _movement.RefreshMovementSpeedModifiers(ent);
        }

        Dirty(ent);
    }

    /// <summary>
    /// Returns the current stamina value for the entity, computed from the snapshot + elapsed regen.
    /// </summary>
    public float GetStamina(Entity<CEStaminaComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return 0f;

        return GetComputedStamina(ent.Comp, _timing.CurTime);
    }

    /// <summary>
    /// Computes the actual stamina from the snapshot value plus any regeneration since RegenStartTime.
    /// </summary>
    private static float GetComputedStamina(CEStaminaComponent comp, TimeSpan curTime)
    {
        if (comp.Stamina >= comp.MaxStamina)
            return comp.MaxStamina;

        if (curTime < comp.RegenStartTime)
            return comp.Stamina;

        var elapsed = (float) (curTime - comp.RegenStartTime).TotalSeconds;
        return Math.Min(comp.Stamina + elapsed * comp.RegenRate, comp.MaxStamina);
    }

    private void TryPopupNotEnough(EntityUid uid, CEStaminaComponent comp)
    {
        var curTime = _timing.CurTime;

        if (curTime < comp.NextPopupTime)
            return;

        comp.NextPopupTime = curTime + TimeSpan.FromSeconds(2);
        _popup.PopupClient(Loc.GetString("ce-stamina-not-enough"), uid, uid, PopupType.SmallCaution);
    }

    /// <summary>
    /// Recalculates effective max stamina by raising <see cref="CECalculateMaxStaminaEvent"/>
    /// (relayed through inventory and status effects), then updates
    /// <see cref="CEStaminaComponent.MaxStamina"/> and scales current stamina proportionally.
    /// </summary>
    public void RefreshMaxStamina(EntityUid uid, CEStaminaComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        var ev = new CECalculateMaxStaminaEvent(comp.BaseMaxStamina);
        RaiseLocalEvent(uid, ev);

        var newMax = MathF.Max(1f, ev.MaxStamina);
        var oldMax = comp.MaxStamina;

        if (MathHelper.CloseTo(newMax, oldMax))
            return;

        // Scale current stamina proportionally to maintain the same fraction.
        if (oldMax > 0f && comp.Stamina > 0f)
            comp.Stamina = MathF.Min(comp.Stamina * newMax / oldMax, newMax);

        comp.MaxStamina = newMax;
        Dirty(uid, comp);
    }

    /// <summary>
    /// Recalculates effective stamina regen rate by raising <see cref="CECalculateStaminaRegenEvent"/>
    /// (relayed through inventory and status effects), then updates
    /// <see cref="CEStaminaComponent.RegenRate"/>.
    /// </summary>
    public void RefreshStaminaRegen(EntityUid uid, CEStaminaComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        var ev = new CECalculateStaminaRegenEvent(comp.BaseRegenRate);
        RaiseLocalEvent(uid, ev);

        var newRate = MathF.Max(0f, ev.RegenRate);

        if (MathHelper.CloseTo(newRate, comp.RegenRate))
            return;

        comp.RegenRate = newRate;
        Dirty(uid, comp);
    }
}

/// <summary>
/// Raised on an entity to calculate its effective maximum stamina.
/// Relayed through inventory (<see cref="IInventoryRelayEvent"/>) and status effects.
/// Handlers can add flat bonuses and multipliers.
/// Final max stamina = (BaseMaxStamina + FlatModifier) * Multiplier.
/// </summary>
public sealed class CECalculateMaxStaminaEvent(float baseMaxStamina) : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;

    public float BaseMaxStamina = baseMaxStamina;
    public float FlatModifier;
    public float Multiplier = 1f;

    public float MaxStamina => (BaseMaxStamina + FlatModifier) * Multiplier;
}

/// <summary>
/// Raised on an entity to calculate its effective stamina regen rate.
/// Relayed through inventory (<see cref="IInventoryRelayEvent"/>) and status effects.
/// Handlers can add flat bonuses and multipliers.
/// Final regen rate = (BaseRegenRate + FlatModifier) * Multiplier.
/// </summary>
public sealed class CECalculateStaminaRegenEvent(float baseRegenRate) : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;

    public float BaseRegenRate = baseRegenRate;
    public float FlatModifier;
    public float Multiplier = 1f;

    public float RegenRate => (BaseRegenRate + FlatModifier) * Multiplier;
}
