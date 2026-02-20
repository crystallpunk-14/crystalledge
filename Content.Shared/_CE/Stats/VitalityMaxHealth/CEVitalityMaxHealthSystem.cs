using Content.Shared._CE.Stats.Core;
using Content.Shared._CE.Stats.Core.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.VitalityMaxHealth;

/// <summary>
/// Handles the connection between Vitality stat and mob health thresholds. Updates critical and death thresholds when vitality changes.
/// </summary>
public sealed partial class CEVitalityMaxHealthSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    private readonly ProtoId<CECharacterStatPrototype> _vitalityStat = "Vitality";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEVitalityMaxHealthComponent, CEStatUpdatedEvent>(OnVitalityUpdated);
    }

    private void OnVitalityUpdated(Entity<CEVitalityMaxHealthComponent> ent, ref CEStatUpdatedEvent args)
    {
        if (args.StatType != _vitalityStat)
            return;

        var critical = args.NewValue * ent.Comp.HealthPerVitality;
        _mobThreshold.SetMobStateThreshold(ent, critical, MobState.Critical);

        var dead = critical * 2;
        _mobThreshold.SetMobStateThreshold(ent, dead, MobState.Dead);
    }
}
