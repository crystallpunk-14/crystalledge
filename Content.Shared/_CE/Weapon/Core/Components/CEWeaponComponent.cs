using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.Weapon.Core.Components;

/// <summary>
/// When given to a mob lets them do unarmed attacks, or when given to an item lets someone wield it to do attacks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
[Access(typeof(CESharedWeaponSystem))]
public sealed partial class CEWeaponComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<CEAttackType, EntProtoId> Attacks = new();

    /// <summary>
    /// Next time this component is allowed to light attack. Heavy attacks are wound up and never have a cooldown.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextAttack;

    /// <summary>
    /// Starts attack cooldown when equipped if true.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ResetOnHandSelected = true;

    /// <summary>
    /// Are we currently holding down the mouse for an attack.
    /// Used so we can't just hold the mouse button and attack constantly.
    /// </summary>
    [AutoNetworkedField]
    public bool Attacking = false;
}

public enum CEAttackType : byte
{
    Primary,
    Secondary
}
