using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects when this entity successfully applies a status effect to a target,
/// then remove stacks of this status effect.
/// The effect target is the entity that received the status effect.
/// If <see cref="SourceStatusEffects"/> is empty, triggers on any status effect application.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnStatusEffectApplyStatusEffectComponent : Component
{
    /// <summary>
    /// Filter: only trigger when one of these status effects is applied.
    /// Leave empty to trigger on any status effect application.
    /// </summary>
    [DataField]
    public List<EntProtoId> SourceStatusEffects = new();

    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;

    [DataField]
    public bool ScaleWithStacks = true;
}
