using Content.Shared._CE.Animation.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Weapon.Core.Components;

/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
[Access(typeof(CESharedWeaponSystem))]
public sealed partial class CEWeaponComponent : Component
{
    /// <summary>
    /// Mapping from input button to attack action prototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<CEAttackType, ProtoId<CEAnimationActionPrototype>> Attacks = new();

    /// <summary>
    /// Are we currently holding down the mouse for an attack.
    /// Used so we can't just hold the mouse button and attack constantly.
    /// </summary>
    [AutoNetworkedField]
    public bool Attacking = false;
}

/// <summary>
/// Which input button binding triggers the attack.
/// </summary>
public enum CEAttackType : byte
{
    Primary,
    Secondary,
}
