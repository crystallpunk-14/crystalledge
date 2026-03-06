using Content.Shared._CE.GOAP;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// GOAP sensor that checks if the entity has a specific status effect active.
/// </summary>
public sealed partial class CEGOAPHasStatusEffectSensor : CEGOAPSensorBase<CEGOAPHasStatusEffectSensor>
{
    /// <summary>
    /// Prototype ID of the status effect entity to check for.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    /// <summary>
    /// Which condition key this sensor updates.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CEGOAPConditionPrototype> ConditionKey;
}

public sealed partial class CEGOAPHasStatusEffectSensorSystem : CEGOAPSensorSystem<CEGOAPHasStatusEffectSensor>
{
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPHasStatusEffectSensor> args)
    {
        var conditionKey = (string) args.Sensor.ConditionKey;
        args.WorldState[conditionKey] = _statusEffect.HasStatusEffect(ent, args.Sensor.StatusEffect);
    }
}
