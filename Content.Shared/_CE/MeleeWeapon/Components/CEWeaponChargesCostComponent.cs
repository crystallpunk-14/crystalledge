using Content.Shared._CE.Animation.Item.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.MeleeWeapon.Components;

/// <summary>
/// When present on a weapon entity, requires charges from <see cref="Charges.CEChargesComponent"/>
/// on the same entity to attack. Costs are defined per <see cref="CEUseType"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEWeaponChargesCostComponent : Component
{
    /// <summary>
    /// Charges consumed per use type.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<CEUseType, int> Costs = new();
}
