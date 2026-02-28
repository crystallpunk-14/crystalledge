namespace Content.Server._CE.NPC.Components;

/// <summary>
/// Runtime marker component for NPC animation-based melee attacks.
/// Tracks whether an attack animation was successfully started.
/// Added by <c>CEMeleeAttackOperator</c> and removed on shutdown.
/// </summary>
[RegisterComponent]
public sealed partial class CENPCMeleeAttackComponent : Component
{
    /// <summary>
    /// The entity being attacked.
    /// </summary>
    [ViewVariables]
    public EntityUid Target;

    /// <summary>
    /// Whether the attack animation was successfully started.
    /// </summary>
    [ViewVariables]
    public bool AnimationStarted;
}
