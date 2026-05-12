using Content.Shared._CE.OneSidePassage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.OneSidePassage;

/// <summary>
/// One-way passage entity (like Dark Souls fog gate).
/// Always lets entities through from the allowed direction; always blocks from the opposite side.
/// Unlike turnstile, has no access check — direction alone determines passage.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CEOneSidePassageComponent : Component
{
    /// <summary>
    /// Maintained set of entities currently passing through (allowed through this frame).
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> CollideExceptions = new();

    /// <summary>
    /// The next time at which the blocked message can show (rate-limit popups).
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextBlockTime;

    /// <summary>
    /// Sound played when an entity is blocked.
    /// </summary>
    [DataField]
    public SoundSpecifier? BlockSound;

    /// <summary>
    /// Sound played when an entity passes through.
    /// </summary>
    [DataField]
    public SoundSpecifier? PassSound;

    /// <summary>
    /// Sprite layer state for idle/default appearance.
    /// </summary>
    [DataField]
    public string DefaultState = "fog";

    /// <summary>
    /// Sprite layer state played when an entity passes through.
    /// </summary>
    [DataField]
    public string PassState = "pass";

    /// <summary>
    /// Sprite layer state played when an entity is blocked.
    /// </summary>
    [DataField]
    public string BlockState = "block";
}

[Serializable, NetSerializable]
public enum CEOneSidePassageVisualLayers : byte
{
    Base
}
