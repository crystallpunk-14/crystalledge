using Robust.Shared.GameStates;

namespace Content.Shared._CE.Charges;

/// <summary>
/// General-purpose charges component. Can be placed on any entity (weapon, tool, artifact).
/// Tracks current and maximum charges.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEChargesComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public int MaxCharges;

    [DataField, AutoNetworkedField]
    public int CurrentCharges;
}
