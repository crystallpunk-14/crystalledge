using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.GOAP.Selectors;
using Content.Shared._CE.Health;

namespace Content.Server._CE.GOAP.Sensors;

[DataDefinition]
public sealed partial class CEGOAPCheckHealthLevelSensorEntry
{
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    [DataField(required: true)]
    public CEGOAPTargetSelector Selector = default!;

    /// <summary>
    /// Health fraction (0..1) below which the condition is set to true.
    /// </summary>
    [DataField]
    public float Threshold = 0.5f;
}

/// <summary>
/// Checks if the entity's own health fraction is below a threshold.
/// Event-driven via CEDamageChangedEvent.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPCheckHealthLevelSensorComponent : Component
{
    [DataField]
    [AlwaysPushInheritance]
    public List<CEGOAPCheckHealthLevelSensorEntry> Entries = [];
}

public sealed partial class CEGOAPCheckHealthLevelSensorSystem : EntitySystem
{
    [Dependency] private CESharedDamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPCheckHealthLevelSensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
        SubscribeLocalEvent<CEGOAPCheckHealthLevelSensorComponent, CEDamageChangedEvent>(OnDamageChanged);
    }

    private void OnRefresh(Entity<CEGOAPCheckHealthLevelSensorComponent> ent, ref CEGOAPSensorRefreshEvent args)
    {
        EvaluateAll(ent);
    }

    private void OnDamageChanged(Entity<CEGOAPCheckHealthLevelSensorComponent> ent, ref CEDamageChangedEvent args)
    {
        EvaluateAll(ent);
    }

    private void EvaluateAll(Entity<CEGOAPCheckHealthLevelSensorComponent> ent)
    {
        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        foreach (var entry in ent.Comp.Entries)
            EvaluateEntry(ent, entry, goap);
    }

    private void EvaluateEntry(EntityUid uid, CEGOAPCheckHealthLevelSensorEntry entry, CEGOAPComponent goap)
    {
        var result = entry.Selector.Resolve(uid, EntityManager);
        if (result.Entity is not { } target)
        {
            goap.WorldState[entry.ConditionKey] = false;
            return;
        }

        var fraction = _damageable.GetHealthInfo(target).Ratio;
        goap.WorldState[entry.ConditionKey] = fraction < entry.Threshold;
    }
}
