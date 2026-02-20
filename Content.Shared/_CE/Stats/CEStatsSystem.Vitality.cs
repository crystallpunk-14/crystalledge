using Content.Shared._CE.Stats.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._CE.Stats;

public sealed partial class CEStatsSystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    private void InitVitality()
    {
        SubscribeLocalEvent<CEVitalityMaxHealthComponent, CEVitalityUpdatedEvent>(OnVitalityUpdated);
    }

    private void OnVitalityUpdated(Entity<CEVitalityMaxHealthComponent> ent, ref CEVitalityUpdatedEvent args)
    {
        var critical = args.Vitality * ent.Comp.HealthPerVitality;
        _mobThreshold.SetMobStateThreshold(ent, critical, MobState.Critical);

        var dead = critical * 2;
        _mobThreshold.SetMobStateThreshold(ent, dead, MobState.Dead);
    }

    public void UpdateVitality(Entity<CEStatsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var ev = new CECalculateVitalityEvent();
        RaiseLocalEvent(ent, ev);

        var vitality = ent.Comp.BaseVitality + (int)(ev.Vitality * ev.Multiplier);
        vitality = Math.Clamp(vitality, 1, 100);
        ent.Comp.Vitality = vitality;
        DirtyField(ent, ent.Comp, nameof(CEStatsComponent.Vitality));

        var ev2 = new CEVitalityUpdatedEvent(vitality);
        RaiseLocalEvent(ent, ev2);
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
