using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.GOAP.Selectors;
using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the entity's mana fraction is below a threshold.
/// Event-driven via CEMagicEnergyLevelChangeEvent.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPCheckManaLevelSensorComponent : Component
{
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    [DataField(required: true)]
    public CEGOAPTargetSelector Selector = default!;

    /// <summary>
    /// Mana fraction (0..1) below which the condition is set to true.
    /// </summary>
    [DataField]
    public float Threshold = 0.5f;
}

public sealed class CEGOAPCheckManaLevelSensorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPCheckManaLevelSensorComponent, CEGOAPSensorRefreshEvent>(OnRefresh);
        SubscribeLocalEvent<CEGOAPCheckManaLevelSensorComponent, CEMagicEnergyLevelChangeEvent>(OnManaChanged);
    }

    private void OnRefresh(Entity<CEGOAPCheckManaLevelSensorComponent> ent, ref CEGOAPSensorRefreshEvent args)
    {
        Evaluate(ent);
    }

    private void OnManaChanged(Entity<CEGOAPCheckManaLevelSensorComponent> ent, ref CEMagicEnergyLevelChangeEvent args)
    {
        Evaluate(ent);
    }

    private void Evaluate(Entity<CEGOAPCheckManaLevelSensorComponent> ent)
    {
        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        var result = ent.Comp.Selector.Resolve(ent, EntityManager);
        if (result.Entity is not { } target)
        {
            goap.WorldState[ent.Comp.ConditionKey] = false;
            return;
        }

        if (!TryComp<CEMagicEnergyContainerComponent>(target, out var mana))
        {
            goap.WorldState[ent.Comp.ConditionKey] = false;
            return;
        }

        var fraction = (float)mana.Energy / mana.MaxEnergy;
        goap.WorldState[ent.Comp.ConditionKey] = fraction < ent.Comp.Threshold;
    }
}
