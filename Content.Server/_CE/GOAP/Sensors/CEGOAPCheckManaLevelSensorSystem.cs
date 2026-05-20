using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the entity's own health is below a threshold.
/// Compares accumulated damage against the critical threshold.
/// </summary>
public sealed partial class CEGOAPCheckManaLevelSensor : CEGOAPSensorBase<CEGOAPCheckManaLevelSensor>
{
    /// <summary>
    /// Health fraction (0..1) below which the condition is set to true.
    /// </summary>
    [DataField]
    public float Threshold = 0.5f;
}

public sealed partial class CEGOAPCheckManaLevelSensorSystem : CEGOAPSensorSystem<CEGOAPCheckManaLevelSensor>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPComponent, CEMagicEnergyLevelChangeEvent>(OnManaChanged);
    }

    /// <summary>
    /// Event-driven update: fires when damage changes on an entity with GOAP.
    /// </summary>
    private void OnManaChanged(Entity<CEGOAPComponent> ent, ref CEMagicEnergyLevelChangeEvent args)
    {
        var newFraction = (float)args.NewValue / (float)args.MaxValue;

        foreach (var sensor in ent.Comp.Sensors)
        {
            if (sensor is not CEGOAPCheckManaLevelSensor healthSensor)
                continue;

            ent.Comp.WorldState[healthSensor.ConditionKey] = newFraction < healthSensor.Threshold;
        }
    }

    protected override bool? OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPCheckManaLevelSensor> args)
    {
        if (!TryComp<CEMagicEnergyContainerComponent>(ent, out var mana))
            return false;

        var fraction = (float)mana.Energy / (float)mana.MaxEnergy;

        return fraction < args.Sensor.Threshold;
    }
}
