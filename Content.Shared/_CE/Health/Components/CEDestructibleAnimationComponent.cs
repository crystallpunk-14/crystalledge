using Content.Shared._CE.Animation.Core.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// When present, the entity will play a death animation before being destroyed.
/// On a fatal hit, <see cref="CEDestructAttemptEvent"/> is cancelled and the animation starts.
/// Once the animation finishes, the entity is destroyed via the normal pipeline.
/// </summary>
[RegisterComponent]
[Access(typeof(CEDestructibleAnimationSystem))]
public sealed partial class CEDestructibleAnimationComponent : Component
{
    /// <summary>
    /// The animation to play on death before the entity is destroyed.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CEEntityEffectAnimationPrototype> Animation;

    /// <summary>
    /// The damage source stored while the animation plays, passed through on final destruction.
    /// </summary>
    public EntityUid? PendingSource;

    /// <summary>
    /// True while the death animation is actively playing.
    /// Prevents the animation from restarting on repeated lethal hits.
    /// </summary>
    public bool IsPlayingDeathAnimation;

    /// <summary>
    /// Set to true once the animation finishes so the next <see cref="CEDestructAttemptEvent"/>
    /// is not cancelled and destruction can actually proceed.
    /// </summary>
    public bool PendingDestruction;
}
