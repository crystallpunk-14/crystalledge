using Content.Shared.Inventory;

namespace Content.Shared._CE.Stats;

public sealed partial class CEStatsSystem
{
    public void UpdateVitality(Entity<CEStatsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var ev = new CECalculateVitalityEvent();
        RaiseLocalEvent(ent, ev);

        var vitality = ent.Comp.BaseVitality + (int)(ev.Vitality * ev.Multiplier);
        SetVitality(ent, vitality);

        var ev2 = new CEVitalityUpdatedEvent(vitality);
        RaiseLocalEvent(ent, ev2);
    }

    private void SetVitality(Entity<CEStatsComponent?> ent, int vitality)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Vitality = vitality;
        Dirty(ent);
    }
}

/// <summary>
/// TODO
/// </summary>
public sealed class CECalculateVitalityEvent : EntityEventArgs, IInventoryRelayEvent
{
    public int Vitality { get; private set; } = 0;
    public float Multiplier { get; private set; } = 1f;

    public void AffectVitality(int amount)
    {
        Vitality += amount;
    }

    public void AffectVitalityMultiplier(float amount)
    {
        Multiplier += amount;
    }

    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}

/// <summary>
///
/// </summary>
public sealed class CEVitalityUpdatedEvent(int vitality) : EntityEventArgs
{
    public int Vitality = vitality;
}
