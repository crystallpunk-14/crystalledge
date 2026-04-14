using Robust.Shared.GameStates;

namespace Content.Shared._CE.Regeneration;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CERegenerationStatusEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Applier;

    [DataField]
    public int Amount = 1;

    /// <summary>
    /// Should damage be scaled based on the number of stacks of this status effect?
    /// </summary>
    [DataField]
    public bool ScaleWithStacks = true;
}
