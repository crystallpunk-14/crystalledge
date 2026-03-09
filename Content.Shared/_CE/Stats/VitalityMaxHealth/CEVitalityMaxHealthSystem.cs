using Content.Shared._CE.Health;
using Content.Shared._CE.Stats.Core;
using Content.Shared._CE.Stats.Core.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Stats.VitalityMaxHealth;

/// <summary>
/// Handles the connection between Vitality stat and max health.
/// Updates <see cref="CESharedHealthSystem.SetMaxHealth"/> when vitality changes.
/// </summary>
public sealed partial class CEVitalityMaxHealthSystem : EntitySystem
{
    [Dependency] private readonly CESharedHealthSystem _health = default!;

    private readonly ProtoId<CECharacterStatPrototype> _vitalityStat = "Vitality";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEVitalityMaxHealthComponent, CEStatUpdatedEvent>(OnVitalityUpdated);
    }

    private void OnVitalityUpdated(Entity<CEVitalityMaxHealthComponent> ent, ref CEStatUpdatedEvent args)
    {
        if (args.StatType != _vitalityStat)
            return;

        var maxHealth = (int)Math.Ceiling(args.NewValue * ent.Comp.HealthPerVitality);
        _health.SetMaxHealth(ent.Owner, maxHealth);
    }
}
