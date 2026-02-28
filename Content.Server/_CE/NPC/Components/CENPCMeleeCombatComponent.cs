using Content.Shared._CE.Animation.Item.Components;
using Content.Shared.Destructible.Thresholds;

namespace Content.Server._CE.NPC.Components;

/// <summary>
/// Runtime marker component for NPC animation-based melee attacks.
/// Tracks whether an attack animation was successfully started.
/// Added by <c>CEMeleeAttackOperator</c> and removed on shutdown.
/// </summary>
[RegisterComponent]
public sealed partial class CENPCMeleeCombatComponent : Component
{
    /// <summary>
    /// The entity being attacked.
    /// </summary>
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public CEUseType UseType = CEUseType.Primary;

    [ViewVariables]
    public float AngleVariation;

    [ViewVariables]
    public CECombatStatus Status = CECombatStatus.Normal;
}

public enum CECombatStatus : byte
{
    /// <summary>
    /// The target isn't in LOS anymore.
    /// </summary>
    NotInSight,

    /// <summary>
    /// Due to some generic reason we are unable to attack the target.
    /// </summary>
    Unspecified,

    /// <summary>
    /// Set if we can't reach the target for whatever reason.
    /// </summary>
    TargetUnreachable,

    /// <summary>
    /// If the target is outside of our melee range.
    /// </summary>
    TargetOutOfRange,

    /// <summary>
    /// Set if the weapon we were assigned is no longer valid.
    /// </summary>
    NoWeapon,

    /// <summary>
    /// No dramas.
    /// </summary>
    Normal,
}
