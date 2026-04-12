using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Health.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Health;

/// <summary>
/// Manages CE damage: application, healing, damage changes.
/// Damage is stored per type in <see cref="CEDamageSpecifier"/>; total is computed.
/// Damage flows through <see cref="CEDamageCalculateEvent"/> for modification before application.
/// </summary>
public abstract partial class CESharedDamageableSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageableComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<CEDamageableComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<DoAfterComponent, CEDamageChangedEvent>(OnDoAfterBreakAttempt);
    }

    private void OnDoAfterBreakAttempt(Entity<DoAfterComponent> ent, ref CEDamageChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!args.InterruptsDoAfters || !args.DamageIncreased)
            return;

        var delta = args.DamageDelta;

        foreach (var (id, doAfter) in ent.Comp.DoAfters)
        {
            if (doAfter.Cancelled || doAfter.Completed)
                continue;

            if (doAfter.Args.BreakOnDamage && delta >= doAfter.Args.DamageThreshold)
                _doAfter.Cancel(ent, id, ent.Comp);
        }
    }

    private void OnRejuvenate(Entity<CEDamageableComponent> ent, ref RejuvenateEvent args)
    {
        SetDamage((ent, ent.Comp), 0);
    }

    private void OnGetState(EntityUid uid, CEDamageableComponent comp, ref ComponentGetState args)
    {
        args.State = new CEDamageableComponentState
        {
            Damage = new CEDamageSpecifier(comp.Damage),
        };
    }

    /// <summary>
    /// Directly changes damage by a delta. Clamped to minimum 0.
    /// Positive delta = more damage, negative delta = healing.
    /// When <paramref name="specifier"/> is provided, per-type amounts are applied.
    /// Otherwise, all existing types are scaled proportionally.
    /// Returns true if damage actually changed.
    /// </summary>
    private bool ChangeDamage(
        Entity<CEDamageableComponent?> ent,
        int delta,
        EntityUid? source = null,
        bool interruptDoAfters = true,
        CEDamageSpecifier? specifier = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        var oldTotal = ent.Comp.Damage.Total;
        var oldDamage = new CEDamageSpecifier(ent.Comp.Damage);

        if (specifier != null)
        {
            // Typed damage: add per-type amounts from the specifier.
            foreach (var (typeId, amount) in specifier.Types)
            {
                if (amount <= 0)
                    continue;

                ent.Comp.Damage.Types.TryGetValue(typeId, out var current);
                ent.Comp.Damage.Types[typeId] = current + amount;
            }
        }
        else if (delta < 0 && oldTotal > 0)
        {
            // Healing without specifier: just reduce types in order until healed.
            var healLeft = Math.Min(oldTotal, -delta);
            foreach (var key in new List<ProtoId<CEDamageTypePrototype>>(ent.Comp.Damage.Types.Keys))
            {
                if (healLeft <= 0)
                    break;

                var val = ent.Comp.Damage.Types[key];
                var heal = Math.Min(healLeft, val);

                if (val - heal > 0)
                    ent.Comp.Damage.Types[key] = val - heal;
                else
                    ent.Comp.Damage.Types.Remove(key);

                healLeft -= heal;
            }
        }
        else if (delta > 0 && oldTotal > 0)
        {
            // Untyped damage increase (e.g. SetDamage scaling): scale types up.
            var targetTotal = oldTotal + delta;
            ScaleDamagePerType(ent.Comp, targetTotal, oldTotal);
        }

        Dirty(ent);

        if (oldDamage.Equals(ent.Comp.Damage))
            return false;

        var ev = new CEDamageChangedEvent(ent, oldDamage, ent.Comp.Damage, source, interruptDoAfters);
        RaiseLocalEvent(ent, ev, true);
        return true;
    }

    /// <summary>
    /// Scales all per-type damage values so that their sum equals <paramref name="targetTotal"/>.
    /// Clears the dictionary when target is 0.
    /// </summary>
    private static void ScaleDamagePerType(CEDamageableComponent comp, int targetTotal, int oldTotal)
    {
        if (targetTotal <= 0)
        {
            comp.Damage.Types.Clear();
            return;
        }

        var scale = (float) targetTotal / oldTotal;
        var keys = new List<ProtoId<CEDamageTypePrototype>>(comp.Damage.Types.Keys);
        foreach (var key in keys)
        {
            var scaled = Math.Max(0, (int) MathF.Round(comp.Damage.Types[key] * scale));
            if (scaled > 0)
                comp.Damage.Types[key] = scaled;
            else
                comp.Damage.Types.Remove(key);
        }
    }

    /// <summary>
    /// Sets damage to an exact value.
    /// </summary>
    public void SetDamage(Entity<CEDamageableComponent?> ent, int damage)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var clamped = Math.Max(0, damage);
        var delta = clamped - ent.Comp.Damage.Total;

        if (delta == 0)
            return;

        ChangeDamage(ent, delta, interruptDoAfters: false);
    }

    /// <summary>
    /// Applies damage specified by <see cref="CEDamageSpecifier"/>.
    /// The total damage (sum of all types) is added to the entity's accumulated damage.
    /// </summary>
    public bool TakeDamage(Entity<CEDamageableComponent?> ent, CEDamageSpecifier damage, EntityUid? source = null, EntityUid? weapon = null, bool ignoreArmor = false, bool interruptDoAfters = true)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        int totalDamage;

        if (ignoreArmor)
            totalDamage = damage.Total;
        else
        {
            var modifiedDamage = new CEDamageSpecifier(damage);

            var beforeEv = new CEDamageCalculateEvent(modifiedDamage, source);
            RaiseLocalEvent(ent, beforeEv);

            if (beforeEv.Cancelled)
                return false;

            totalDamage = beforeEv.Damage.Total;
        }

        if (totalDamage <= 0)
            return false;

        // Build the final specifier reflecting armor modifications.
        CEDamageSpecifier? finalSpecifier = null;
        if (damage.Types.Count > 0 && damage.Total > 0)
        {
            var ratio = totalDamage / (float) damage.Total;
            finalSpecifier = damage * ratio;
        }

        var changed = ChangeDamage(ent, totalDamage, source, interruptDoAfters, finalSpecifier);

        if (changed)
            RaiseDamageEffect(ent, source);

        return changed;
    }

    /// <summary>
    /// Heals the entity by reducing accumulated damage.
    /// </summary>
    public void Heal(Entity<CEDamageableComponent?> target, int amount, EntityUid? source = null)
    {
        if (!Resolve(target, ref target.Comp, false))
            return;

        var finalAmount = amount;
        if (source is not null)
        {
            var getHealEv = new CEGetHealAmountEvent(target, amount);
            RaiseLocalEvent(source.Value, getHealEv);

            finalAmount = getHealEv.HealAmount;

            var attemptHealEv = new CEAttemptHealEvent(target, finalAmount);
            RaiseLocalEvent(source.Value, attemptHealEv);

            if (attemptHealEv.Cancelled)
                return;
        }

        var incomingHealEv = new CEGetIncomingHealEvent(finalAmount);
        RaiseLocalEvent(target, incomingHealEv);
        finalAmount = incomingHealEv.HealAmount;

        if (finalAmount <= 0)
            return;

        ChangeDamage(target, -finalAmount, interruptDoAfters: false);
    }

    /// <summary>
    /// Returns health info for any damageable entity.
    /// Uses <see cref="CEMobStateComponent.CriticalThreshold"/> when available,
    /// falls back to <see cref="CEDestructibleComponent.DestroyThreshold"/> for entities without mob state.
    /// </summary>
    public CEHealthInfo GetHealthInfo(EntityUid uid)
    {
        if (!TryComp<CEDamageableComponent>(uid, out var damage))
            return new CEHealthInfo { CurrentHp = 0, MaxHp = 0, Ratio = 1f };

        if (TryComp<CEMobStateComponent>(uid, out var mobState))
        {
            var maxHp = mobState.CriticalThreshold;
            var currentHp = Math.Max(0, maxHp - damage.Damage.Total);

            int? destroyThreshold = null;
            int? remainingUntilDeath = null;
            if (TryComp<CEDestructibleComponent>(uid, out var destr))
            {
                destroyThreshold = destr.DestroyThreshold;
                remainingUntilDeath = Math.Max(0, maxHp + destr.DestroyThreshold - damage.Damage.Total);
            }

            return new CEHealthInfo
            {
                CurrentHp = currentHp,
                MaxHp = maxHp,
                Ratio = maxHp > 0 ? Math.Clamp((float) currentHp / maxHp, 0f, 1f) : 0f,
                MobState = mobState.CurrentState,
                HasMobState = true,
                DestroyThreshold = destroyThreshold,
                RemainingUntilDeath = remainingUntilDeath,
            };
        }

        if (TryComp<CEDestructibleComponent>(uid, out var destructible) && destructible.DestroyThreshold > 0)
        {
            var maxHp = destructible.DestroyThreshold;
            var currentHp = Math.Max(0, maxHp - damage.Damage.Total);

            return new CEHealthInfo
            {
                CurrentHp = currentHp,
                MaxHp = maxHp,
                Ratio = Math.Clamp((float) currentHp / maxHp, 0f, 1f),
                HasMobState = false,
            };
        }

        return new CEHealthInfo { CurrentHp = 0, MaxHp = 0, Ratio = 1f };
    }

    /// <summary>
    /// Raises visual and audio effects for damage on an entity.
    /// Server sends via PVS, client shows locally during prediction.
    /// </summary>
    protected virtual void RaiseDamageEffect(EntityUid target, EntityUid? source)
    {
    }
}

/// <summary>
/// Raised when damage changes on an entity.
/// Carries per-type old and new <see cref="CEDamageSpecifier"/> snapshots.
/// Subscribers can compute per-type deltas for colored popups via <c>.OldDamage.Types</c> / <c>.NewDamage.Types</c>.
/// </summary>
public sealed class CEDamageChangedEvent : EntityEventArgs
{
    public readonly EntityUid Target;
    public readonly CEDamageSpecifier OldDamage;
    public readonly CEDamageSpecifier NewDamage;
    public readonly EntityUid? Source;

    /// <summary>
    /// True when the event was raised from game logic (ChangeDamage / prediction),
    /// false when it was raised from HandleState (authoritative server correction).
    /// </summary>
    public readonly bool Predicted;

    /// <summary>
    /// Was any of the damage change dealing damage, or was it all healing?
    /// </summary>
    public readonly bool DamageIncreased;

    /// <summary>
    /// Does this event interrupt DoAfters?
    /// Accounts for <see cref="DamageIncreased"/>: only true when damage actually went up.
    /// </summary>
    public readonly bool InterruptsDoAfters;

    public int DamageDelta => NewDamage.Total - OldDamage.Total;

    public CEDamageChangedEvent(
        EntityUid target,
        CEDamageSpecifier oldDamage,
        CEDamageSpecifier newDamage,
        EntityUid? source = null,
        bool interruptsDoAfters = true,
        bool predicted = true)
    {
        Target = target;
        OldDamage = oldDamage;
        NewDamage = newDamage;
        Source = source;
        Predicted = predicted;

        // True if ANY individual type increased, even if the total went down.
        var damageIncreased = false;
        foreach (var (typeId, newAmount) in newDamage.Types)
        {
            oldDamage.Types.TryGetValue(typeId, out var oldAmount);
            if (newAmount > oldAmount)
            {
                damageIncreased = true;
                break;
            }
        }

        DamageIncreased = damageIncreased;
        InterruptsDoAfters = interruptsDoAfters && DamageIncreased;
    }
}

/// <summary>
/// Raised before damage is applied. Systems can modify <see cref="Damage"/> or cancel the event.
/// </summary>
public sealed class CEDamageCalculateEvent(CEDamageSpecifier damage, EntityUid? source) : EntityEventArgs, IInventoryRelayEvent
{
    public CEDamageSpecifier Damage = damage;
    public EntityUid? Source = source;
    public bool Cancelled;

    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}

/// <summary>
/// Called on healing source entity to calculate the amount to heal.
/// </summary>
public sealed class CEGetHealAmountEvent(EntityUid target, int healAmount) : EntityEventArgs
{
    public EntityUid Target = target;
    public int HealAmount = healAmount;
}

/// <summary>
/// Raised on an entity that is trying to heal another entity. Can be cancelled.
/// </summary>
public sealed class CEAttemptHealEvent(EntityUid target, int healAmount) : CancellableEntityEventArgs
{
    public readonly EntityUid Target = target;
    public readonly int HealAmount = healAmount;
}

/// <summary>
/// Raised on an entity to calculate its effective maximum health.
/// Relayed through inventory (<see cref="IInventoryRelayEvent"/>) and status effects.
/// Handlers can add flat bonuses and multipliers.
/// Final max health = (BaseMaxHealth + FlatModifier) * Multiplier.
/// </summary>
public sealed class CECalculateMaxHealthEvent(int baseMaxHealth) : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;

    public int BaseMaxHealth = baseMaxHealth;
    public int FlatModifier;
    public float Multiplier = 1f;

    public int MaxHealth => (int)((BaseMaxHealth + FlatModifier) * Multiplier);
}

/// <summary>
/// Raised on the target entity to allow inventory items to modify incoming healing.
/// </summary>
public sealed class CEGetIncomingHealEvent(int healAmount) : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
    public int HealAmount = healAmount;
}

/// <summary>
/// Snapshot of an entity's health state. Works for entities with or without <see cref="CEMobStateComponent"/>.
/// </summary>
public struct CEHealthInfo
{
    public int CurrentHp;
    public int MaxHp;
    public float Ratio;
    public bool HasMobState;
    public CEMobState MobState;
    public int? DestroyThreshold;
    public int? RemainingUntilDeath;
}
