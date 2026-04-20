using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.RemoveStackOnHeal;

[RegisterComponent, NetworkedComponent]
public sealed partial class CERemoveStackOnHealComponent : Component
{
    [DataField]
    public int Amount = 1;

    /// <summary>
    /// If false, cant remove last status effect stack
    /// </summary>
    [DataField]
    public bool CanRemoveStatusEffect = true;
}
