using Content.Shared._CE.Stats.Core;
using Content.Shared._CE.Stats.Core.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._CE.Stats.VitalityMaxHealth;

public sealed partial class CEVitalityMaxHealthSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEVitalityMaxHealthComponent, CEStatUpdatedEvent>(OnVitalityUpdated);
    }

    private void OnVitalityUpdated(Entity<CEVitalityMaxHealthComponent> ent, ref CEStatUpdatedEvent args)
    {
        if (args.StatType != "Vitality") //TODO unhardcode
            return;

        var critical = args.NewValue * ent.Comp.HealthPerVitality;
        _mobThreshold.SetMobStateThreshold(ent, critical, MobState.Critical);

        var dead = critical * 2;
        _mobThreshold.SetMobStateThreshold(ent, dead, MobState.Dead);
    }
}
