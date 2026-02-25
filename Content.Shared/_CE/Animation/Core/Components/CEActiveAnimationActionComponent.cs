using Content.Shared._CE.Animation.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Core.Components;

/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEActiveAnimationActionComponent : Component
{
    [DataField]
    public ProtoId<CEAnimationActionPrototype>? ActiveAnimation;

    [DataField]
    public TimeSpan? StartAnimationTime;

    /// <summary>
    /// Current animation angle
    /// </summary>
    [DataField]
    public Angle? AnimationAngle;
}
