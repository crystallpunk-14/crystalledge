using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared._CE.TempShield;
using Content.Shared.Examine;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Consumes all temporary shield stacks on the caster and deals area damage
/// equal to those stacks. Each shield type maps to its corresponding damage type.
/// The caster is not affected by the explosion. Damage does not pass through walls.
/// </summary>
public sealed partial class ShieldBurst : CEEntityEffectBase<ShieldBurst>
{
    [DataField]
    public float Range = 2f;

    [DataField]
    public EntProtoId? Vfx;
}

public sealed partial class CEShieldBurstEffectSystem : CEEntityEffectSystem<ShieldBurst>
{
    [Dependency] private readonly CEStatusEffectStackSystem _stacks = default!;
    [Dependency] private readonly CESharedDamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    protected override void Effect(ref CEEntityEffectEvent<ShieldBurst> args)
    {
        var caster = args.Args.Source;

        if (!TryComp<StatusEffectContainerComponent>(caster, out var container))
            return;

        var effects = container.ActiveStatusEffects?.ContainedEntities;
        if (effects == null)
            return;

        // Collect shield stacks across all active temp shield effects.
        var damage = new CEDamageSpecifier();
        var totalStacks = 0;

        // Snapshot the list — we will remove effects during iteration.
        var snapshot = new List<EntityUid>(effects);

        foreach (var effectEnt in snapshot)
        {
            if (!TryComp<CETempShieldStatusEffectComponent>(effectEnt, out var shield))
                continue;

            var stacks = _stacks.GetStack(effectEnt);
            if (stacks <= 0)
                continue;

            // Map stacks to absorbed damage types. If AbsorbedTypes is empty, skip.
            foreach (var damageType in shield.AbsorbedTypes)
            {
                damage.Types.TryGetValue(damageType, out var existing);
                damage.Types[damageType] = existing + stacks;
            }

            totalStacks += stacks;

            // Remove this shield entirely.
            _stacks.TryRemoveStack(effectEnt, stacks);
        }

        if (totalStacks <= 0)
            return;

        // Spawn VFX on the caster.
        if (args.Effect.Vfx is { } vfx)
            SpawnAtPosition(vfx, Transform(caster).Coordinates);

        // Deal area damage centered on the caster, excluding the caster.
        // Respects line-of-sight (does not pass through walls).
        var center = _transform.GetMapCoordinates(caster);
        var nearby = _lookup.GetEntitiesInRange<CEDamageableComponent>(center, args.Effect.Range, LookupFlags.Uncontained);

        foreach (var entity in nearby)
        {
            if (entity.Owner == caster)
                continue;

            var targetPos = _transform.GetMapCoordinates(entity.Owner);
            if (!_examine.InRangeUnOccluded(center, targetPos, args.Effect.Range, null))
                continue;

            _damageable.TakeDamage(entity.Owner, damage, caster);
        }
    }
}
