using Robust.Shared.GameStates;

namespace Content.Shared._CE.Regeneration;

[RegisterComponent, NetworkedComponent]
public sealed partial class CERegenerationStatusEffectComponent : Component
{
    [DataField]
    public int Amount = 1;

    /// <summary>
    /// Should healing be scaled based on the number of stacks of this status effect?
    /// </summary>
    [DataField]
    public bool ScaleWithStacks = true;
}
