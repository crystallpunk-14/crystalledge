using Content.Shared._CE.Animation.Item.Components;

namespace Content.Shared._CE.GOAP.Actions;

/// <summary>
/// Performs a melee attack on the current target.
/// Uses absolute coordinates for steering to ensure proper pathfinding.
/// </summary>
public sealed partial class CEGOAPMeleeAttackAction : CEGOAPActionBase<CEGOAPMeleeAttackAction>
{
    [DataField]
    public CEUseType UseType = CEUseType.Primary;

    /// <summary>
    /// Random angle spread for attacks in degrees.
    /// </summary>
    [DataField]
    public float AngleVariation = 15f;

    /// <summary>
    /// Minimal distance to the target to perform the attack.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// How far the target must move before re-registering steering.
    /// </summary>
    [DataField]
    public float ReregisterThreshold = 1.5f;
}
