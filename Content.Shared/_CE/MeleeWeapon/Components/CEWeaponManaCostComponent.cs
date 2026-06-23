using Content.Shared._CE.Animation.Item.Components;
using Content.Shared.Power.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.MeleeWeapon.Components;

/// <summary>
/// When present on a weapon entity, requires mana energy from
/// <see cref="BatteryComponent"/>
/// on the same entity to attack. Costs are defined per <see cref="CEUseType"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEWeaponManaCostComponent : Component
{
    /// <summary>
    /// Mana energy consumed per use type.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<CEUseType, int> Costs = new();
}
