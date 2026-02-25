using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Weapon;

/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEMeleeWeaponComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public DamageSpecifier Damage;
}
