using Content.Shared._CE.Health.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.DamageModifier;

/// <summary>
/// A generic status effect component that modifies incoming damage.
/// Supports two modification modes per damage type:
/// <list type="bullet">
///   <item><b>Flat modifiers</b> — add or subtract a fixed amount from the damage value.</item>
///   <item><b>Multiplier modifiers</b> — multiply the damage value (0 = immunity, 0.5 = 50% reduction, 2.0 = double damage).</item>
/// </list>
/// Flat modifiers are applied first, then multipliers.
/// Place this component on a status effect entity so it receives
/// <c>StatusEffectRelayedEvent&lt;CEBeforeDamageEvent&gt;</c>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEDamageModifierStatusEffectComponent : Component
{
    /// <summary>
    /// Flat damage modifiers per damage type.
    /// Positive values increase incoming damage, negative values reduce it.
    /// The resulting damage per type is clamped to a minimum of 0.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<CEDamageTypePrototype>, int> FlatModifiers = new();

    /// <summary>
    /// Multiplier modifiers per damage type.
    /// Applied after flat modifiers. 0 = full immunity, 0.5 = halved, 2.0 = doubled.
    /// The resulting damage per type is clamped to a minimum of 0.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<CEDamageTypePrototype>, float> Multipliers = new();
}
