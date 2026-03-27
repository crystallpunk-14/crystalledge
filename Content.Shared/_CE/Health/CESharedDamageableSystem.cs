using Content.Shared._CE.Health.Components;
using Content.Shared.Inventory;
using Content.Shared.Rejuvenate;

namespace Content.Shared._CE.Health;

/// <summary>
/// Manages CE damage: application, healing, damage changes.
/// Damage is a single int that starts at 0 and increases.
/// Damage flows through <see cref="CEDamageCalculateEvent"/> for modification before application.
/// </summary>
public abstract partial class CESharedDamageableSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageableComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<CEDamageableComponent> ent, ref RejuvenateEvent args)
    {
        SetDamage((ent, ent.Comp), 0);
    }

    /// <summary>
    /// Directly changes damage by a delta. Clamped to minimum 0.
    /// Positive delta = more damage, negative delta = healing.
    /// </summary>
    public void ChangeDamage(Entity<CEDamageableComponent?> ent, int delta, out int actualDelta, EntityUid? source = null)
    {
        actualDelta = 0;

        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var oldDamage = ent.Comp.TotalDamage;
        var newDamage = Math.Max(0, ent.Comp.TotalDamage + delta);

        actualDelta = newDamage - oldDamage;
        ent.Comp.TotalDamage = newDamage;
        Dirty(ent);

        if (oldDamage != newDamage)
        {
            var ev = new CEDamageChangedEvent(ent, oldDamage, newDamage, source);
            RaiseLocalEvent(ent, ev, true);
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
        var delta = clamped - ent.Comp.TotalDamage;

        if (delta == 0)
            return;

        ChangeDamage(ent, delta, out _);
    }

    /// <summary>
    /// Applies damage specified by <see cref="CEDamageSpecifier"/>.
    /// The total damage (sum of all types) is added to the entity's accumulated damage.
    /// </summary>
    public bool TakeDamage(Entity<CEDamageableComponent?> ent, CEDamageSpecifier damage, EntityUid? source = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        var modifiedDamage = new CEDamageSpecifier(damage);

        var beforeEv = new CEDamageCalculateEvent(modifiedDamage, source);
        RaiseLocalEvent(ent, beforeEv);

        if (beforeEv.Cancelled)
            return false;

        var totalDamage = beforeEv.Damage.Total;
        if (totalDamage <= 0)
            return false;

        ChangeDamage(ent, totalDamage, out var actualDelta, source);

        if (actualDelta != 0)
            RaiseDamageEffect(ent, source);

        return actualDelta != 0;
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

        if (finalAmount <= 0)
            return;

        ChangeDamage(target, -finalAmount, out _);
    }

    public int GetDamage(Entity<CEDamageableComponent?> target)
    {
        if (!Resolve(target, ref target.Comp, false))
            return 0;

        return target.Comp.TotalDamage;
    }

    /// <summary>
    /// Raises a red color flash on the damaged entity.
    /// Server sends via PVS (excluding source), client shows locally during prediction.
    /// </summary>
    protected virtual void RaiseDamageEffect(EntityUid target, EntityUid? source)
    {
    }
}

/// <summary>
/// Raised when damage changes on an entity.
/// </summary>
public sealed class CEDamageChangedEvent(EntityUid target, int oldDamage, int newDamage, EntityUid? source = null)
    : EntityEventArgs
{
    public readonly EntityUid Target = target;
    public readonly int OldDamage = oldDamage;
    public readonly int NewDamage = newDamage;
    public readonly EntityUid? Source = source;
    public int DamageDelta => NewDamage - OldDamage;
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
