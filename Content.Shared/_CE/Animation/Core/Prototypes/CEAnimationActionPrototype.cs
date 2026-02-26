using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Core.Prototypes;

/// <summary>
/// An alert popup with associated icon, tooltip, and other data.
/// </summary>
[Prototype("animationAction")]
public sealed partial class CEAnimationActionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Character movement speed modifier while this animation is active
    /// </summary>
    [DataField]
    public float MovementSpeed = 1f;

    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.Zero;

    /// <summary>
    /// Blocks the character's ability to turn while the animation is active
    /// </summary>
    [DataField]
    public bool LockRotation = true;

    [DataField]
    public Dictionary<TimeSpan, List<CEAnimationActionEntry>> Events = new();
}
