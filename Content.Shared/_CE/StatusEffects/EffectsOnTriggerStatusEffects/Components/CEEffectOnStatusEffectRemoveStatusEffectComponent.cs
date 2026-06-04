using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects when a status effect this entity's owner applied to a target is removed.
/// The effect User is the owner of this status effect, Target is the entity the effect was removed from.
/// If <see cref="SourceStatusEffects"/> is empty, triggers on any status effect removal.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnStatusEffectRemoveStatusEffectComponent : Component
{
    /// <summary>
    /// Filter: only trigger when one of these status effects is removed.
    /// Leave empty to trigger on any status effect removal.
    /// </summary>
    [DataField]
    public List<EntProtoId> SourceStatusEffects = new();

    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;
}

