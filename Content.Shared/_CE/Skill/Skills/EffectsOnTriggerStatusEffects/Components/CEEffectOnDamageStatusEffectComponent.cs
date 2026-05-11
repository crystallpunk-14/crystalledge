using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.Health;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects to the damage target when the owner of this status effect deals damage,
/// optionally filtering by <see cref="CEAttackType"/>.
/// Consumes <see cref="StackCost"/> stacks of this status effect per proc.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnDamageStatusEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost;

    /// <summary>
    /// If non-empty, only trigger when the attack type matches one of these values.
    /// Empty means trigger for all attack types.
    /// </summary>
    [DataField]
    public HashSet<CEAttackType> AttackTypes = new();
}
