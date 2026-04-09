using Content.Shared._CE.Animation.Item.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.MeleeWeapon.Components;

/// <summary>
/// When present on a weapon entity, requires the user to spend mana to attack.
/// Costs are defined per <see cref="CEUseType"/> (Primary, Secondary, etc.).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEWeaponManaCostComponent : Component
{
    /// <summary>
    /// Mana cost per use type.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<CEUseType, int> Costs = new();
}
