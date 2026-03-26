using Content.Server._CE.GOAP;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._CE.GOAPAlarm;

/// <summary>
/// Added to entities that are currently selected as a GOAP target.
/// Tracks which GOAP agents reference this entity and under which target keys.
/// Automatically managed by <see cref="CEGOAPSystem.SetTarget"/>.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPAlarmComponent : Component
{
    [DataField]
    public EntProtoId AlarmVFX = "CEAlarmEffect";

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10f);

    [DataField(customTypeSerializer:typeof(TimeOffsetSerializer))]
    public TimeSpan LastAlarm = TimeSpan.Zero;

    [DataField]
    public float Radius = 3f;
}
