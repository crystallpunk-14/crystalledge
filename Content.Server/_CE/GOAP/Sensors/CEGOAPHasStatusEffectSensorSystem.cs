using Content.Shared._CE.GOAP;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the entity has a specific status effect active.
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

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPHasStatusEffectSensor> args)
    {
        SetState(ref args, _statusEffect.HasStatusEffect(ent, args.Sensor.StatusEffect));
    }
}
