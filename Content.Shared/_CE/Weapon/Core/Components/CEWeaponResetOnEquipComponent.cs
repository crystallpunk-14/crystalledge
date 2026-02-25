using Robust.Shared.GameStates;

namespace Content.Shared._CE.Weapon.Core.Components;

/// <summary>
/// Component that marks a weapon to have its state reset when it is equipped.
/// The reset can only occur at most once per <see cref="Cooldown"/> interval.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CESharedWeaponSystem))]
public sealed partial class CEWeaponResetOnEquipComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);
}
