using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.GOAP.Selectors;
using Content.Shared._CE.StatusEffects;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the entity has a specific status effect active.
/// Event-driven: reacts to CE status effect applied/removed events on this entity.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPHasStatusEffectSensorComponent : Component
{
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    [DataField(required: true)]
    public CEGOAPTargetSelector Selector = default!;

    /// <summary>
    /// Prototype ID of the status effect entity to check for.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId StatusEffect;
}

public sealed class CEGOAPHasStatusEffectSensorSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPHasStatusEffectSensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
        SubscribeLocalEvent<CEGOAPHasStatusEffectSensorComponent, CEStatusEffectAppliedToEntityEvent>(OnEffectApplied);
        SubscribeLocalEvent<CEGOAPHasStatusEffectSensorComponent, CEStatusEffectRemovedFromEntityEvent>(OnEffectRemoved);
    }

    private void OnRefresh(Entity<CEGOAPHasStatusEffectSensorComponent> ent, ref CEGOAPSensorRefreshEvent args)
    {
        Evaluate(ent);
    }

    private void OnEffectApplied(Entity<CEGOAPHasStatusEffectSensorComponent> ent, ref CEStatusEffectAppliedToEntityEvent args)
    {
        Evaluate(ent);
    }

    private void OnEffectRemoved(Entity<CEGOAPHasStatusEffectSensorComponent> ent, ref CEStatusEffectRemovedFromEntityEvent args)
    {
        Evaluate(ent);
    }

    private void Evaluate(Entity<CEGOAPHasStatusEffectSensorComponent> ent)
    {
        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        var result = ent.Comp.Selector.Resolve(ent, EntityManager);
        if (result.Entity is not { } target)
        {
            goap.WorldState[ent.Comp.ConditionKey] = false;
            return;
        }

        goap.WorldState[ent.Comp.ConditionKey] = _statusEffect.HasStatusEffect(target, ent.Comp.StatusEffect);
    }
}
