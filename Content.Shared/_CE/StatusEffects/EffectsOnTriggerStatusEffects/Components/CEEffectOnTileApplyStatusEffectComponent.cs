using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects when the entity applies a tile effect, then remove stacks of this status effect.
/// If <see cref="SourceTileEffects"/> is empty, triggers on any tile effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnTileApplyStatusEffectComponent : Component
{
    /// <summary>
    /// Filter: only trigger when one of these tile effects is applied.
    /// Leave empty to trigger on any tile effect.
    /// </summary>
    [DataField]
    public List<EntProtoId> SourceTileEffects = new();

    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;

    [DataField]
    public bool ScaleWithStacks = true;
}
