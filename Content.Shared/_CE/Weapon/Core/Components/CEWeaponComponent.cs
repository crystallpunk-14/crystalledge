using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.Weapon.Core.Components;

/// <summary>
/// When given to a mob lets them do unarmed attacks, or when given to an item lets someone wield it to do attacks.
/// Attack actions are defined by <see cref="CEAttackActionPrototype"/> and mapped to input buttons.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
[Access(typeof(CESharedWeaponSystem))]
public sealed partial class CEWeaponComponent : Component
{
    /// <summary>
    /// Mapping from input button to attack action prototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<CEAttackType, ProtoId<CEAttackActionPrototype>> Attacks = new();

    /// <summary>
    /// Next time this component is allowed to attack.
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

    /// <summary>
    /// Alternates between left and right swing for animations.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SwingLeft;

    /// <summary>
    /// Whether to alternate swing direction each attack.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SwingBeverage = true;
}

/// <summary>
/// Which input button binding triggers the attack.
/// </summary>
public enum CEAttackType : byte
{
    Primary,
    Secondary,
}
