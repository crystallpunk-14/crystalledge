using Content.Server._CE.GOAP.Classifiers;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;

namespace Content.Server._CE.GOAP.Sensors;

[RegisterComponent]
public sealed partial class CEGOAPHasEnemySensorComponent : Component
{
    [DataField(required: true)]
    public string ConditionKey = string.Empty;
}

public sealed class CEGOAPHasEnemySensorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPHasEnemySensorComponent, CEGOAPKnowledgeCacheRebuiltEvent>(OnCacheRebuilt);
        SubscribeLocalEvent<CEGOAPHasEnemySensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
    }

    private void OnCacheRebuilt(Entity<CEGOAPHasEnemySensorComponent> ent, ref CEGOAPKnowledgeCacheRebuiltEvent args)
    {
        Evaluate(ent);
    }

    private void OnRefresh(Entity<CEGOAPHasEnemySensorComponent> ent, ref CEGOAPSensorRefreshEvent args)
    {
        Evaluate(ent);
    }

    private void Evaluate(Entity<CEGOAPHasEnemySensorComponent> ent)
    {
        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        if (!TryComp<CEGOAPKnowledgeCacheComponent>(ent, out var cache))
        {
            goap.WorldState[ent.Comp.ConditionKey] = false;
            return;
        }

        goap.WorldState[ent.Comp.ConditionKey] = cache.Enemies.Count > 0;
    }
}
