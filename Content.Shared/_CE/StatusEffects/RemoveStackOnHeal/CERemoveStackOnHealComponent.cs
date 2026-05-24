using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.RemoveStackOnHeal;

[RegisterComponent, NetworkedComponent]
public sealed partial class CERemoveStackOnHealComponent : Component
{
    /// <summary>
    /// How many heal units are required to remove 1 stack.
    /// </summary>
    [DataField]
    public int HealPerStack = 1;

    /// <summary>
    /// If false, cant remove last status effect stack
    /// </summary>
    [DataField]
    public bool CanRemoveStatusEffect = true;
}
