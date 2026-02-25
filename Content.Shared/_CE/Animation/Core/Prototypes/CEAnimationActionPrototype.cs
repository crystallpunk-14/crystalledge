using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Core.Prototypes;

/// <summary>
/// An alert popup with associated icon, tooltip, and other data.
/// </summary>
[Prototype]
public sealed partial class CEAnimationActionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float MovementSpeed = 1f;

    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.Zero;

    [DataField]
    public bool LockRotation = true;
}
