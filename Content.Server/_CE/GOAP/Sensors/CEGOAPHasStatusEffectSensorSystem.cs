using Content.Shared._CE.GOAP;
using Content.Shared._CE.StatusEffects;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the entity has a specific status effect active.
/// Event-driven: reacts to CE status effect events raised on the target entity.
/// </summary>
public sealed partial class CEGOAPHasStatusEffectSensor : CEGOAPSensorBase<CEGOAPHasStatusEffectSensor>
{
    /// <summary>
    /// Prototype ID of the status effect entity to check for.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId StatusEffect;
}

public sealed partial class CEGOAPHasStatusEffectSensorSystem : CEGOAPSensorSystem<CEGOAPHasStatusEffectSensor>
{
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPComponent, CEStatusEffectAppliedToEntityEvent>(OnEffectApplied);
        SubscribeLocalEvent<CEGOAPComponent, CEStatusEffectRemovedFromEntityEvent>(OnEffectRemoved);
    }

    private void OnEffectApplied(Entity<CEGOAPComponent> ent, ref CEStatusEffectAppliedToEntityEvent args)
    {
        RefreshSensors(ent);
    }

    private void OnEffectRemoved(Entity<CEGOAPComponent> ent, ref CEStatusEffectRemovedFromEntityEvent args)
    {
        RefreshSensors(ent);
    }

    private void RefreshSensors(Entity<CEGOAPComponent> ent)
    {
        foreach (var sensor in ent.Comp.Sensors)
        {
            if (sensor is not CEGOAPHasStatusEffectSensor statusSensor)
                continue;

            ent.Comp.WorldState[statusSensor.ConditionKey] =
                _statusEffect.HasStatusEffect(ent, statusSensor.StatusEffect);
        }
    }

    protected override bool OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPHasStatusEffectSensor> args)
    {
        return _statusEffect.HasStatusEffect(ent, args.Sensor.StatusEffect);
    }
}
