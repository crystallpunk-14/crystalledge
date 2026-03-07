using Content.Shared._CE.Health;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.DamageStatusEffect.Components;

/// <summary>
/// Deals damage to the entity each time a status effect is applied or the number of stacks of that status effect is updated.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEDamageStatusEffectComponent : Component
{
    [DataField(required: true)]
    public CEDamageSpecifier Damage;

    /// <summary>
    /// Should damage be scaled based on the number of stacks of this status effect?
    /// </summary>
    [DataField]
    public bool ScaleWithStacks = true;
}
