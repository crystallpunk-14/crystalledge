using Content.Shared._CE.Animation.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.Animation.Core.Components;

/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEActiveAnimationActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<CEAnimationActionPrototype>? ActiveAnimation;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan? StartAnimationTime;

    /// <summary>
    /// Current animation angle
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle? AnimationAngle;

    [DataField, AutoNetworkedField]
    public bool LockRotation;

    [DataField, AutoNetworkedField]
    public EntityUid? Used;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan LastEvent = TimeSpan.FromSeconds(-1);
}
