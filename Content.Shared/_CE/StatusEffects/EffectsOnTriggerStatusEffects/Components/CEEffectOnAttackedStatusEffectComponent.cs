using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnAttackedStatusEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;

    [DataField]
    public bool ScaleWithStacks = true;
}
