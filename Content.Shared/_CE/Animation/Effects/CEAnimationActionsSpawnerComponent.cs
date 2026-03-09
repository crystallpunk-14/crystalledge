using Content.Shared._CE.Animation.Core;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.Animation.Effects;

/// <summary>
///
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CEAnimationActionsSpawnerComponent : Component
{
    [DataField]
    public List<CEAnimationActionEntry> Effects = default!;

    [DataField]
    public TimeSpan FirstDelay = TimeSpan.FromSeconds(1f);

    [DataField]
    public TimeSpan Frequency = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextEffectTime = TimeSpan.Zero;
}
