using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Health.Prototypes;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Examine;
using Robust.Shared.Map;
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
}

public sealed partial class CEShieldBurstEffectSystem : CEEntityEffectSystem<ShieldBurst>
{
    [Dependency] private readonly CEStatusEffectStackSystem _stacks = default!;
    [Dependency] private readonly CESharedDamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    /// <summary>
    /// Maps temp shield status effects back to their damage types.
    /// </summary>
    private static readonly Dictionary<EntProtoId, ProtoId<CEDamageTypePrototype>> ShieldToDamage = new()
    {
        { "CEStatusEffectTempShield", "Physical" },
        { "CEStatusEffectTempShieldFire", "Fire" },
        { "CEStatusEffectTempShieldCold", "Cold" },
    };

    protected override void Effect(ref CEEntityEffectEvent<ShieldBurst> args)
    {
        var caster = args.Args.Source;

        // Collect all shield stacks on the caster.
        var damage = new CEDamageSpecifier();
        var totalStacks = 0;

        foreach (var (shieldEffect, damageType) in ShieldToDamage)
        {
            var stackCount = _stacks.GetStack(caster, shieldEffect);
            if (stackCount <= 0)
                continue;

            damage.Types[damageType] = stackCount;
            totalStacks += stackCount;

            // Remove all stacks of this shield.
            _stacks.TryRemoveStack(caster, shieldEffect, stackCount);
        }

        if (totalStacks <= 0)
            return;

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
