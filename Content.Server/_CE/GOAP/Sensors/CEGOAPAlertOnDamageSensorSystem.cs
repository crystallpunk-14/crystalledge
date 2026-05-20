using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Event-driven sensor that sets a target when the GOAP entity takes damage and has no current target.
/// Useful for allowing mobs to react to ranged attackers who are outside normal vision range.
/// </summary>
public sealed partial class CEGOAPAlertOnDamageSensor : CEGOAPSensorBase<CEGOAPAlertOnDamageSensor>
{
    /// <summary>
    /// Key in <see cref="CEGOAPComponent.Targets"/> to write the damage source into.
    /// </summary>
    [DataField(required: true)]
    public string OutputTargetKey = string.Empty;

    /// <summary>
    /// When true, only sets the target if there is no existing target for <see cref="OutputTargetKey"/>.
    /// Set to false to always override the current target on damage.
    /// </summary>
    [DataField]
    public bool OnlyWhenNoTarget = true;
}

public sealed partial class CEGOAPAlertOnDamageSensorSystem : CEGOAPSensorSystem<CEGOAPAlertOnDamageSensor>
{
    [Dependency] private readonly EntityQuery<CEGOAPComponent> _goapQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        // TODO: OK we really need redesign GOAP sensors at that point, multiple sensors wanna subscribes to same events.
        SubscribeLocalEvent<CEDamageChangedEvent>(OnDamage);
    }

    private void OnDamage(CEDamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Source is not { } source)
            return;

        if (!_goapQuery.TryGetComponent(args.Target, out var goap))
            return;

        Entity<CEGOAPComponent> ent = (args.Target, goap);

        foreach (var sensor in goap.Sensors)
        {
            if (sensor is not CEGOAPAlertOnDamageSensor alertSensor)
                continue;

            if (alertSensor.OnlyWhenNoTarget && Goap.GetTarget(ent, alertSensor.OutputTargetKey) != null)
                continue;

            Goap.SetTarget(ent, alertSensor.OutputTargetKey, source);
        }
    }
}
