using Robust.Shared.GameStates;

namespace Content.Shared._CE.Weapon.Core.Components;

/// <summary>
/// TODO
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CESharedWeaponSystem))]
public sealed partial class CEWeaponResetOnEquipComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);
}
