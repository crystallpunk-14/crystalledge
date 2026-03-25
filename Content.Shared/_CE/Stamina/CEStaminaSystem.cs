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
}
