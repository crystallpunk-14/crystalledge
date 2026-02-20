using Content.Shared._CE.Stats.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._CE.Stats;

public sealed partial class CEStatsSystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    private void InitVitality()
    {
        SubscribeLocalEvent<CEVitalityMaxHealthComponent, CEStatUpdatedEvent>(OnVitalityUpdated);
    }

    private void OnVitalityUpdated(Entity<CEVitalityMaxHealthComponent> ent, ref CEStatUpdatedEvent args)
    {
        if (args.StatType != CEStatType.Vitality)
            return;

        var critical = args.NewValue * ent.Comp.HealthPerVitality;
        _mobThreshold.SetMobStateThreshold(ent, critical, MobState.Critical);

        var dead = critical * 2;
        _mobThreshold.SetMobStateThreshold(ent, dead, MobState.Dead);
    }

    public void UpdateVitality(Entity<CEStatsComponent?> ent)
    {
        UpdateStatValue(ent, CEStatType.Vitality);
    }
}
