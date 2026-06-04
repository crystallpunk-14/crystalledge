using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects when the entity receives souls, then remove stacks of this status effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnSoulReceivedStatusEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;

    [DataField]
    public bool ScaleWithStacks = true;
}
