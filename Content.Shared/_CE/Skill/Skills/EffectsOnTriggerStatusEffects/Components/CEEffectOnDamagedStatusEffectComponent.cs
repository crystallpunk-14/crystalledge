using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects when the entity takes damage, then remove stacks of this status effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnDamagedStatusEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost;
}
