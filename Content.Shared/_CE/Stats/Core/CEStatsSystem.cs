using Content.Shared._CE.Stats.Core.Components;
using Content.Shared._CE.Stats.Core.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

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

    public void UpdateStatValue(Entity<CEStatsComponent?> ent, ProtoId<CECharacterStatPrototype> statType)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var calcEvent = new CECalculateStatEvent(statType);
        RaiseLocalEvent(ent, calcEvent);

        var baseStat = ent.Comp.BaseStats.GetValueOrDefault(statType, 0);
        var oldValue = ent.Comp.Stats.GetValueOrDefault(statType, 0);

        var newValue = (int)Math.Ceiling((baseStat + calcEvent.Value) * (calcEvent.Multiplier - 1));
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
public sealed class CECalculateStatEvent(ProtoId<CECharacterStatPrototype> statType) : EntityEventArgs, IInventoryRelayEvent
{
    public ProtoId<CECharacterStatPrototype> StatType { get; private set; } = statType;
    public int Value { get; private set; } = 0;
    public float Multiplier { get; private set; } = 1f;

    public void AffectValue(int amount)
    {
        Value += amount;
    }

    /// <summary>
    /// Change the parameter value as a percentage. 0 = do not change. 1 = increase by 100%
    /// </summary>
    /// <param name="amount"></param>
    public void AffectMultiplier(float amount)
    {
        Multiplier *= amount;
    }

    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}

/// <summary>
/// This event is triggered when the value of a characteristic has been updated.
/// </summary>
public sealed class CEStatUpdatedEvent(ProtoId<CECharacterStatPrototype> statType, int oldValue, int newValue) : EntityEventArgs
{
    public ProtoId<CECharacterStatPrototype> StatType = statType;
    public int OldValue = oldValue;
    public int NewValue = newValue;
}
