using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects when an entity falls on the bearer of this status effect.
/// User is the entity that was fallen on; Target is the entity that fell.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnFallenOnStatusEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;

    [DataField]
    public bool ScaleWithStacks = true;

    /// <summary>
    /// When true, Power in CEEntityEffectArgs is set to the fall impact speed.
    /// </summary>
    [DataField]
    public bool ScaleWithSpeed = false;
}
