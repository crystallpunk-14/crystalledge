using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks whether a last-known position exists for a given target key.
/// Returns true if LastKnownPositions contains the key AND the target is currently lost.
/// Event-driven via CETargetChangedEvent on the GOAP entity.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPHasLastKnownPositionSensorComponent : Component
{
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    /// <summary>
    /// The target key to check in LastKnownPositions.
    /// </summary>
    [DataField(required: true)]
    public string PositionTargetKey = string.Empty;
}

public sealed class CEGOAPHasLastKnownPositionSensorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPHasLastKnownPositionSensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
        SubscribeLocalEvent<CEGOAPHasLastKnownPositionSensorComponent, CETargetChangedEvent>(OnTargetChanged);
        SubscribeLocalEvent<CEGOAPHasLastKnownPositionSensorComponent, CEGOAPKnowledgeUpdatedEvent>(OnKnowledgeUpdated);
    }

    private void OnKnowledgeUpdated(Entity<CEGOAPHasLastKnownPositionSensorComponent> ent, ref CEGOAPKnowledgeUpdatedEvent args)
    {
        Evaluate(ent);
    }

    private void OnRefresh(Entity<CEGOAPHasLastKnownPositionSensorComponent> ent, ref CEGOAPSensorRefreshEvent args)
    {
        Evaluate(ent);
    }

    private void OnTargetChanged(Entity<CEGOAPHasLastKnownPositionSensorComponent> ent, ref CETargetChangedEvent args)
    {
        if (ent.Comp.PositionTargetKey != args.TargetKey)
            return;

        Evaluate(ent);
    }

    private void Evaluate(Entity<CEGOAPHasLastKnownPositionSensorComponent> ent)
    {
        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        var key = ent.Comp.PositionTargetKey;

        if (!goap.LastKnownPositions.ContainsKey(key))
        {
            goap.WorldState[ent.Comp.ConditionKey] = false;
            return;
        }

        // Only true if the target is currently lost.
        if (goap.Targets.TryGetValue(key, out var target) && target != null)
        {
            goap.WorldState[ent.Comp.ConditionKey] = false;
            return;
        }

        goap.WorldState[ent.Comp.ConditionKey] = true;
    }
}
