using Content.Shared._CE.Stats.Core.Components;
using Content.Shared._CE.Stats.Core.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.Core;

/// <summary>
/// Manages the character stats system, handling stat calculations, updates, and modifier application.
/// </summary>
public sealed partial class CEStatsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        InitClothing();
        InitStatusEffects();

        SubscribeLocalEvent<CEStatsComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CEStatsComponent> ent, ref MapInitEvent args)
    {
        foreach (var stat in ent.Comp.BaseStats)
        {
            RecalculateStat((ent, ent.Comp), stat.Key);
        }
    }

    /// <summary>
    /// Recalculates the value of a specific stat for an entity, applying all modifiers.
    /// </summary>
    public void RecalculateStat(Entity<CEStatsComponent?> ent, ProtoId<CECharacterStatPrototype> statType)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var calcEvent = new CECalculateStatEvent(statType);
        RaiseLocalEvent(ent, calcEvent);

        var baseStat = ent.Comp.BaseStats.GetValueOrDefault(statType, 0);
        var oldValue = ent.Comp.Stats.GetValueOrDefault(statType, 0);

        var newValue = (int)Math.Ceiling((baseStat + calcEvent.Value) * calcEvent.Multiplier);
        newValue = Math.Clamp(newValue, 1, 100);
        ent.Comp.Stats[statType] = newValue;
        Dirty(ent);

        if (oldValue == newValue)
            return;

        var updateEvent = new CEStatUpdatedEvent(statType, oldValue, newValue);
        RaiseLocalEvent(ent, updateEvent);
    }
}

/// <summary>
/// Event raised when a character stat value needs to be recalculated. Allows systems to apply modifiers.
/// </summary>
public sealed class CECalculateStatEvent(ProtoId<CECharacterStatPrototype> statType) : EntityEventArgs, IInventoryRelayEvent
{
    /// <summary>
    /// The type of stat being calculated.
    /// </summary>
    public ProtoId<CECharacterStatPrototype> StatType { get; private set; } = statType;

    /// <summary>
    /// Flat additive modifier to apply to the stat value.
    /// </summary>
    public int Value { get; private set; } = 0;

    /// <summary>
    /// Multiplicative modifier to apply to the stat value.
    /// </summary>
    public float Multiplier { get; private set; } = 1f;

    /// <summary>
    /// Adds a flat amount to the stat value.
    /// </summary>
    public void AffectValue(int amount)
    {
        Value += amount;
    }

    /// <summary>
    /// Applies a multiplicative modifier to the stat value. Use 0.1 for -90%, 1.1 for +10%.
    /// </summary>
    public void AffectMultiplier(float amount)
    {
        Multiplier *= amount;
    }

    /// <summary>
    /// Inventory slots to relay this event to when calculating stats.
    /// </summary>
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}

/// <summary>
/// Event raised when a character stat value has been updated to a new value.
/// </summary>
public sealed class CEStatUpdatedEvent(ProtoId<CECharacterStatPrototype> statType, int oldValue, int newValue) : EntityEventArgs
{
    /// <summary>
    /// The type of stat that was updated.
    /// </summary>
    public ProtoId<CECharacterStatPrototype> StatType = statType;

    /// <summary>
    /// The stat value before the update.
    /// </summary>
    public int OldValue = oldValue;

    /// <summary>
    /// The stat value after the update.
    /// </summary>
    public int NewValue = newValue;
}
