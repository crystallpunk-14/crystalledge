using Content.Shared._CE.Health.Components;
using Content.Shared.Rejuvenate;

namespace Content.Shared._CE.Health;

/// <summary>
/// Manages CrystallEdge health: damage application, healing, max health changes.
/// Health is a single int value. Damage is specified via <see cref="CEDamageSpecifier"/>
/// and flows through <see cref="CEBeforeDamageEvent"/> for modification before application.
/// </summary>
public abstract partial class CESharedHealthSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        InitBlocker();
        InitMobState();

        SubscribeLocalEvent<CEHealthComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<CEHealthComponent> ent, ref RejuvenateEvent args)
    {
        ChangeHealth((ent, ent.Comp), ent.Comp.MaxHealth - ent.Comp.Health, out _);
    }

    /// <summary>
    /// Directly changes health by a delta. Clamps between <see cref="CEHealthComponent.DeathThreshold"/>
    /// and <see cref="CEHealthComponent.MaxHealth"/>.
    /// </summary>
    public void ChangeHealth(Entity<CEHealthComponent?> ent, int delta, out int actualDelta)
    {
        actualDelta = 0;

        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var oldHealth = ent.Comp.Health;
        var newHealth = Math.Clamp(ent.Comp.Health + delta, ent.Comp.DeathThreshold, ent.Comp.MaxHealth);

        actualDelta = newHealth - oldHealth;
        ent.Comp.Health = newHealth;
        Dirty(ent);

        if (oldHealth != newHealth)
        {
            var ev = new CEHealthChangedEvent(ent, oldHealth, newHealth, ent.Comp.MaxHealth);
            RaiseLocalEvent(ent, ev, true);
        }
    }

    /// <summary>
    /// Applies damage specified by <see cref="CEDamageSpecifier"/>.
    /// The total damage (sum of all types) is subtracted from health.
    /// </summary>
    public bool TakeDamage(Entity<CEHealthComponent?> ent, CEDamageSpecifier damage, EntityUid? source = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        var modifiedDamage = new CEDamageSpecifier(damage);

        var beforeEv = new CEBeforeDamageEvent(modifiedDamage, source);
        RaiseLocalEvent(ent, beforeEv);

        if (beforeEv.Cancelled)
            return false;

        var totalDamage = beforeEv.Damage.Total;
        if (totalDamage <= 0)
            return false;

        ChangeHealth(ent, -totalDamage, out var actualDelta);
        return actualDelta != 0;
    }

    /// <summary>
    /// Heals the entity by the specified amount.
    /// </summary>
    public void Heal(Entity<CEHealthComponent?> target, int amount, EntityUid? source = null)
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

        ChangeHealth(target, finalAmount, out _);
    }

    /// <summary>
    /// Sets the maximum health. Current health is scaled proportionally,
    /// identical to how mana works in <see cref="CESharedMagicEnergySystem"/>.
    /// </summary>
    public void SetMaxHealth(Entity<CEHealthComponent?> ent, int maxHealth)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.MaxHealth == maxHealth)
            return;

        var oldHealth = ent.Comp.Health;
        var oldMax = ent.Comp.MaxHealth;

        ent.Comp.MaxHealth = maxHealth;

        if (oldMax > 0)
        {
            ent.Comp.Health = (int)((long)oldHealth * maxHealth / oldMax);
            ent.Comp.Health = Math.Clamp(ent.Comp.Health, ent.Comp.DeathThreshold, ent.Comp.MaxHealth);
        }
        else
        {
            ent.Comp.Health = Math.Min(ent.Comp.Health, ent.Comp.MaxHealth);
        }

        Dirty(ent);

        var ev = new CEHealthChangedEvent(ent, oldHealth, ent.Comp.Health, ent.Comp.MaxHealth);
        RaiseLocalEvent(ent, ev, true);
    }

    public bool HasHealth(Entity<CEHealthComponent?> target)
    {
        return Resolve(target, ref target.Comp, false);
    }
}

/// <summary>
/// Raised when health changes on an entity.
/// </summary>
public sealed class CEHealthChangedEvent(EntityUid target, int oldHealth, int newHealth, int maxHealth)
    : EntityEventArgs
{
    public readonly EntityUid Target = target;
    public readonly int OldHealth = oldHealth;
    public readonly int NewHealth = newHealth;
    public readonly int MaxHealth = maxHealth;
}

/// <summary>
/// Raised before damage is applied. Systems can modify <see cref="Damage"/> or cancel the event.
/// </summary>
public sealed class CEBeforeDamageEvent(CEDamageSpecifier damage, EntityUid? source) : EntityEventArgs
{
    public CEDamageSpecifier Damage = damage;
    public EntityUid? Source = source;
    public bool Cancelled;
}

/// <summary>
/// Called on healing source entity to calculate the amount to heal. Systems can modify the heal amount.
/// </summary>
public sealed class CEGetHealAmountEvent(EntityUid target, int healAmount) : EntityEventArgs
{
    public EntityUid Target = target;
    public int HealAmount = healAmount;
}

/// <summary>
/// Raised on an entity that is trying to heal another entity.
/// Systems can inspect the potential healing amount and the target entity that is getting healed.
/// Systems can cancel the heal.
/// </summary>
/// <remarks>
/// Raised when <see cref="CESharedHealthSystem.Heal"/>is called and a source is supplied.
/// </remarks>
public sealed class CEAttemptHealEvent(EntityUid target, int healAmount) : CancellableEntityEventArgs
{
    public readonly EntityUid Target = target;
    public readonly int HealAmount = healAmount;
}
