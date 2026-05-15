using Content.Shared._CE.Animation.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.GOAPAlarm;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEGOAPAlarmAnimationComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CEEntityEffectAnimationPrototype> Animation = string.Empty;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10f);

    [DataField(customTypeSerializer:typeof(TimeOffsetSerializer))]
    public TimeSpan LastAlarm = TimeSpan.Zero;

    [DataField]
    public float Radius = 3f;
}
