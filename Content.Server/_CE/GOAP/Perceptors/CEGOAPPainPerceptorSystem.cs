using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health;

namespace Content.Server._CE.GOAP.Perceptors;

/// <summary>
/// Pain perception. Whenever the NPC takes damage from a known source, that source is added
/// to the knowledge store. Lets mobs react to attackers they didn't visually spot first.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPPainPerceptorComponent : Component
{
    /// <summary>
    /// How long an attacker stays in knowledge after the last hit.
    /// </summary>
    [DataField]
    public TimeSpan MemoryDuration = TimeSpan.FromSeconds(15);
}

public sealed class CEGOAPPainPerceptorSystem : EntitySystem
{
    [Dependency] private readonly CEGOAPSystem _goap = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPPainPerceptorComponent, CEDamageChangedEvent>(OnDamaged);
    }

    private void OnDamaged(Entity<CEGOAPPainPerceptorComponent> ent, ref CEDamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (args.Source is not { } source || source == ent.Owner)
            return;

        if (!TryComp<CEGOAPComponent>(ent, out var goap))
            return;

        _goap.Remember(
            (ent.Owner, goap),
            source,
            Transform(source).Coordinates,
            ent.Comp.MemoryDuration);
    }
}
