using Content.Shared._CE.Stats.Core.Components;
using Content.Shared.Inventory;

namespace Content.Shared._CE.Stats.Core;

public sealed partial class CEStatsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        InitClothing();

        SubscribeLocalEvent<CEStatsComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CEStatsComponent> ent, ref MapInitEvent args)
    {
        foreach (var stat in ent.Comp.BaseStats)
        {
            UpdateStatValue((ent, ent.Comp), stat.Key);
        }
    }

    public void UpdateStatValue(Entity<CEStatsComponent?> ent, CEStatType statType)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var calcEvent = new CECalculateStatEvent(statType);
        RaiseLocalEvent(ent, calcEvent);

        var baseStat = ent.Comp.BaseStats.GetValueOrDefault(statType, 1);
        var oldValue = ent.Comp.Stats.GetValueOrDefault(statType, 1);

        var newValue = baseStat + (int)(calcEvent.Value * calcEvent.Multiplier);
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
/// This event is triggered when the current value of a character's characteristic needs to be recalculated.
/// </summary>
public sealed class CECalculateStatEvent(CEStatType statType) : EntityEventArgs, IInventoryRelayEvent
{
    public CEStatType StatType { get; private set; } = statType;
    public int Value { get; private set; } = 1;
    public float Multiplier { get; private set; } = 1f;

    public void AffectValue(int amount)
    {
        Value += amount;
    }

    public void AffectMultiplier(float amount)
    {
        Multiplier += amount;
    }

    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}

/// <summary>
/// This event is triggered when the value of a characteristic has been updated.
/// </summary>
public sealed class CEStatUpdatedEvent(CEStatType statType, int oldValue, int newValue) : EntityEventArgs
{
    public CEStatType StatType = statType;
    public int OldValue = oldValue;
    public int NewValue = newValue;
}
