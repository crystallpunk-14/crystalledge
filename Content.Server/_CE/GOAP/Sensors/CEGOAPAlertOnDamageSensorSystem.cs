using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Event-driven sensor that sets a target when the GOAP entity takes damage and has no current target.
/// Useful for allowing mobs to react to ranged attackers who are outside normal vision range.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPAlertOnDamageSensorComponent : Component
{
    /// <summary>
    /// Key in CEGOAPComponent.Targets to write the damage source into.
    /// </summary>
    [DataField(required: true)]
    public string OutputTargetKey = string.Empty;

    /// <summary>
    /// When true, only sets the target if there is no existing target for OutputTargetKey.
    /// </summary>
    [DataField]
    public bool OnlyWhenNoTarget = true;
}

public sealed class CEGOAPAlertOnDamageSensorSystem : EntitySystem
{
    [Dependency] private readonly CEGOAPSystem _goap = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPAlertOnDamageSensorComponent, CEDamageChangedEvent>(OnDamage);
    }

    private void OnDamage(Entity<CEGOAPAlertOnDamageSensorComponent> ent, ref CEDamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Source is not { } source)
            return;

        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        Entity<CEGOAPComponent> goapEnt = (ent.Owner, goap);

        if (ent.Comp.OnlyWhenNoTarget && _goap.GetTarget(goapEnt, ent.Comp.OutputTargetKey) != null)
            return;

        _goap.SetTarget(goapEnt, ent.Comp.OutputTargetKey, source);
    }
}
