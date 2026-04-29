using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.TileEffects.Core;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[EntityCategory("StatusEffects")]
public sealed partial class CETileEffectComponent : Component
{
    /// <summary>
    /// Current number of stacks. At 0, the entity is deleted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Stacks = 1;

    [DataField]
    public int MaxStacks = 10;

    /// <summary>
    /// Amount by which Stacks changes each decay tick. Negative = decays, positive = grows, 0 = stable.
    /// </summary>
    [DataField]
    public int StackDelta = -1;

    /// <summary>
    /// Minimum seconds between decay ticks.
    /// </summary>
    [DataField]
    public float MinDecayInterval = 2f;

    /// <summary>
    /// Maximum seconds between decay ticks.
    /// </summary>
    [DataField]
    public float MaxDecayInterval = 5f;

    /// <summary>
    /// Who created this tile effect?
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Applier;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// Only entities passing this whitelist receive contact effects. Null = all entities pass.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Entities matching this blacklist are excluded from contact effects. Null = none blocked.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// Stack threshold for medium visual appearance.
    /// </summary>
    [DataField]
    public int MediumThreshold = 3;

    /// <summary>
    /// Stack threshold for high visual appearance.
    /// </summary>
    [DataField]
    public int HighThreshold = 6;
}
