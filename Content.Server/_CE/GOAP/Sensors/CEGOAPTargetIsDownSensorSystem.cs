using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the current target is incapacitated (critical or dead).
/// Event-driven via CEGOAPTargetComponent: reacts to CEMobStateChangedEvent on tracked targets.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPTargetIsDownSensorComponent : Component
{
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    [DataField(required: true)]
    public string TargetKey = string.Empty;
}

public sealed class CEGOAPTargetIsDownSensorSystem : EntitySystem
{
    [Dependency] private readonly CEGOAPSystem _goap = default!;
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPTargetIsDownSensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
        SubscribeLocalEvent<CEGOAPTargetComponent, CEMobStateChangedEvent>(OnTargetMobStateChanged);
    }

    private void OnRefresh(Entity<CEGOAPTargetIsDownSensorComponent> ent, ref CEGOAPSensorRefreshEvent args)
    {
        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        Evaluate((ent.Owner, goap), ent.Comp);
    }

    private void OnTargetMobStateChanged(Entity<CEGOAPTargetComponent> ent, ref CEMobStateChangedEvent args)
    {
        foreach (var (goapUid, keys) in ent.Comp.Trackers)
        {
            if (!TryComp<CEGOAPComponent>(goapUid, out var goap))
                continue;

            if (!TryComp<CEGOAPTargetIsDownSensorComponent>(goapUid, out var sensor))
                continue;

            if (!keys.Contains(sensor.TargetKey))
                continue;

            goap.WorldState[sensor.ConditionKey] = !_mobState.IsAlive(ent.Owner);
        }
    }

    private void Evaluate(Entity<CEGOAPComponent> ent, CEGOAPTargetIsDownSensorComponent sensor)
    {
        var target = _goap.GetTarget(ent, sensor.TargetKey);
        ent.Comp.WorldState[sensor.ConditionKey] = target != null && !_mobState.IsAlive(target.Value);
    }
}
