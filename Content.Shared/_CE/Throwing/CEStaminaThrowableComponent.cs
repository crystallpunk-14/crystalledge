using Robust.Shared.GameStates;

namespace Content.Shared._CE.Throwing;


/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEStaminaThrowableComponent : Component
{
    [DataField]
    public float Cost = 1f;
}
