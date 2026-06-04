using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects when the owner of this status effect lands (hits floor/ceiling from Z-level movement).
/// The effect User and Target are both the entity that landed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnLandingStatusEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;
}
