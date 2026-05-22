using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects on entitiesss that you heals, then remove stacks of this status effect
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnHealStatusEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;
}
